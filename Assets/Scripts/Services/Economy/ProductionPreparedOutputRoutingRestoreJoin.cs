using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct ProductionPreparedOutputRouteRestoreAcknowledgement
{
    public ProductionPreparedOutputRouteRestoreAcknowledgement(
        string routeOperationId,
        string physicalReceiptFingerprint)
    {
        RouteOperationId = routeOperationId ?? string.Empty;
        PhysicalReceiptFingerprint = physicalReceiptFingerprint ?? string.Empty;
    }

    public string RouteOperationId { get; }
    public string PhysicalReceiptFingerprint { get; }
}

public sealed class ProductionPreparedOutputRoutingRestoreJoinPlan
{
    public ProductionPreparedOutputRoutingRestoreJoinPlan(
        ProductionPreparedOutputRoutingSaveData candidate,
        IReadOnlyList<ProductionPreparedOutputPhysicalRouteReceipt>
            physicalCommits,
        IReadOnlyList<ProductionPreparedOutputRouteRestoreAcknowledgement>
            acknowledgements,
        bool joinValidated)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        PhysicalCommits = Array.AsReadOnly((physicalCommits
                ?? Array.Empty<ProductionPreparedOutputPhysicalRouteReceipt>())
            .ToArray());
        Acknowledgements = Array.AsReadOnly((acknowledgements
                ?? Array.Empty<ProductionPreparedOutputRouteRestoreAcknowledgement>())
            .ToArray());
        JoinValidated = joinValidated;
    }

    public ProductionPreparedOutputRoutingSaveData Candidate { get; }
    public IReadOnlyList<ProductionPreparedOutputPhysicalRouteReceipt>
        PhysicalCommits { get; }
    public IReadOnlyList<ProductionPreparedOutputRouteRestoreAcknowledgement>
        Acknowledgements { get; }
    public bool JoinValidated { get; }
}

public interface IFacilityOutputExactRouteRestoreReconciler
{
    void AcknowledgeRestoredRoute(
        string routeOperationId,
        string physicalReceiptFingerprint);
}

public interface IProductionPreparedOutputRoutingRestoreReconciler
{
    void CommitRestoredPhysicalRoute(
        ProductionPreparedOutputPhysicalRouteReceipt receipt);

    void AcknowledgeRestoredRoute(
        string routeOperationId,
        string physicalReceiptFingerprint);
}

public interface IProductionPreparedOutputRoutingRestoreJoin
{
    ProductionPreparedOutputRoutingRestoreJoinPlan Build(
        ProductionPreparedOutputRoutingSaveData candidate);

    void Reconcile(ProductionPreparedOutputRoutingRestoreJoinPlan plan);
}

public sealed class ProductionPreparedOutputRoutingRestoreJoin :
    IProductionPreparedOutputRoutingRestoreJoin
{
    private readonly IFacilityOutputExactRouteRestoreCandidateQuery items;
    private readonly IFacilityOutputExactRouteRestoreReconciler itemReconciler;
    private readonly IProductionPreparedOutputRoutingRestoreReconciler
        ownerReconciler;

    public ProductionPreparedOutputRoutingRestoreJoin(
        IFacilityOutputExactRouteRestoreCandidateQuery items,
        IFacilityOutputExactRouteRestoreReconciler itemReconciler,
        IProductionPreparedOutputRoutingRestoreReconciler ownerReconciler)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.itemReconciler = itemReconciler
            ?? throw new ArgumentNullException(nameof(itemReconciler));
        this.ownerReconciler = ownerReconciler
            ?? throw new ArgumentNullException(nameof(ownerReconciler));
    }

    public ProductionPreparedOutputRoutingRestoreJoinPlan Build(
        ProductionPreparedOutputRoutingSaveData candidate)
    {
        if (candidate?.batches == null)
            throw new InvalidOperationException(
                "Prepared-output routing restore has no owner collection.");
        if (!items.IsCandidateAvailable)
        {
            return new ProductionPreparedOutputRoutingRestoreJoinPlan(
                candidate,
                Array.Empty<ProductionPreparedOutputPhysicalRouteReceipt>(),
                Array.Empty<ProductionPreparedOutputRouteRestoreAcknowledgement>(),
                joinValidated: false);
        }
        if (candidate.lastConfirmedCheckpointSequence !=
                items.LastConfirmedCheckpointSequence
            || !string.Equals(
                candidate.lastConfirmedCheckpointDigest,
                items.LastConfirmedCheckpointDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Prepared-output checkpoint authority conflicts across Economy and Items restore candidates.");
        }

        Dictionary<string, FacilityOutputExactRouteOutboxSaveData> physical =
            new(StringComparer.Ordinal);
        foreach (FacilityOutputExactRouteOutboxSaveData route in
                 items.Routes ?? Array.Empty<FacilityOutputExactRouteOutboxSaveData>())
        {
            if (route == null
                || !physical.TryAdd(route.routeOperationId, route))
            {
                throw new InvalidOperationException(
                    "Physical exact-output-route candidate has a null or duplicate operation.");
            }
        }

        HashSet<string> owners = new(StringComparer.Ordinal);
        List<ProductionPreparedOutputPhysicalRouteReceipt> physicalCommits =
            new();
        List<ProductionPreparedOutputRouteRestoreAcknowledgement> acknowledge =
            new();
        foreach (ProductionPreparedOutputRoutingBatchSaveData batch in
                 candidate.batches)
        {
            if (batch?.lines == null)
                throw new InvalidOperationException(
                    "Prepared-output routing restore has a null batch or line collection.");
            foreach (ProductionPreparedOutputRoutingLineSaveData line in
                     batch.lines)
            {
                ValidateLineCoverage(batch, line);
                foreach (ProductionPreparedOutputRouteOperationSaveData owner in
                         line.routeOperations)
                {
                    if (owner == null
                        || !owners.Add(owner.routeOperationId))
                    {
                        throw new InvalidOperationException(
                            "Prepared-output routing restore has a null or duplicate route owner.");
                    }
                    bool hasPhysical = physical.TryGetValue(
                        owner.routeOperationId,
                        out FacilityOutputExactRouteOutboxSaveData route);
                    if (owner.phase == ProductionPreparedOutputRoutePhase.PhysicalPending)
                    {
                        if (!hasPhysical)
                            continue;
                        if (route.phase != FacilityOutputExactRoutePhase.PhysicalPending)
                            throw new InvalidOperationException(
                                $"Physical-pending Economy route '{owner.routeOperationId}' has an impossible acknowledged Items receipt.");
                        ProductionPreparedOutputPhysicalRouteReceipt recovered =
                            ReconstructPhysicalReceipt(batch, line, owner, route);
                        physicalCommits.Add(recovered);
                        acknowledge.Add(
                            new ProductionPreparedOutputRouteRestoreAcknowledgement(
                                owner.routeOperationId,
                                recovered.PhysicalReceiptFingerprint));
                        continue;
                    }
                    if (!hasPhysical)
                        throw new InvalidOperationException(
                            $"Applied route '{owner.routeOperationId}' has no exact Items receipt.");

                    ValidateExactRoute(batch, line, owner, route);
                    bool phaseMatches = owner.phase switch
                    {
                        ProductionPreparedOutputRoutePhase
                                .PhysicalAppliedAwaitingItemsAck =>
                            route.phase is FacilityOutputExactRoutePhase.PhysicalPending
                                or FacilityOutputExactRoutePhase.Routable,
                        ProductionPreparedOutputRoutePhase
                                .ItemsAcknowledgedAwaitingCheckpointGc =>
                            route.phase == FacilityOutputExactRoutePhase.Routable,
                        _ => throw new InvalidOperationException(
                            "Prepared-output routing restore has an unsupported route phase.")
                    };
                    if (!phaseMatches)
                        throw new InvalidOperationException(
                            $"Route '{owner.routeOperationId}' phase conflicts across Economy and Items.");
                    if (owner.phase == ProductionPreparedOutputRoutePhase
                            .PhysicalAppliedAwaitingItemsAck)
                    {
                        acknowledge.Add(
                            new ProductionPreparedOutputRouteRestoreAcknowledgement(
                                owner.routeOperationId,
                                owner.physicalReceiptFingerprint));
                    }
                }
            }
        }

        string orphan = physical.Keys
            .Where(operation => !owners.Contains(operation))
            .OrderBy(operation => operation, StringComparer.Ordinal)
            .FirstOrDefault();
        if (orphan != null)
            throw new InvalidOperationException(
                $"Physical exact-output-route '{orphan}' has no Economy owner.");

        return new ProductionPreparedOutputRoutingRestoreJoinPlan(
            candidate,
            physicalCommits
                .OrderBy(value => value.RouteOperationId, StringComparer.Ordinal)
                .ToArray(),
            acknowledge
                .OrderBy(value => value.RouteOperationId, StringComparer.Ordinal)
                .ToArray(),
            joinValidated: true);
    }

    public void Reconcile(ProductionPreparedOutputRoutingRestoreJoinPlan plan)
    {
        if (plan == null || !plan.JoinValidated)
            throw new InvalidOperationException(
                "Prepared-output routing restore cannot publish without its physical join.");
        foreach (ProductionPreparedOutputPhysicalRouteReceipt receipt in
                 plan.PhysicalCommits)
        {
            ownerReconciler.CommitRestoredPhysicalRoute(receipt);
        }
        foreach (ProductionPreparedOutputRouteRestoreAcknowledgement value in
                 plan.Acknowledgements)
        {
            itemReconciler.AcknowledgeRestoredRoute(
                value.RouteOperationId,
                value.PhysicalReceiptFingerprint);
            ownerReconciler.AcknowledgeRestoredRoute(
                value.RouteOperationId,
                value.PhysicalReceiptFingerprint);
        }
    }

    private static ProductionPreparedOutputPhysicalRouteReceipt
        ReconstructPhysicalReceipt(
            ProductionPreparedOutputRoutingBatchSaveData batch,
            ProductionPreparedOutputRoutingLineSaveData line,
            ProductionPreparedOutputRouteOperationSaveData owner,
            FacilityOutputExactRouteOutboxSaveData route)
    {
        if (!string.Equals(owner.routeOperationId, route.routeOperationId,
                StringComparison.Ordinal)
            || !string.Equals(owner.requestFingerprint,
                route.requestFingerprint, StringComparison.Ordinal)
            || !string.IsNullOrEmpty(owner.physicalReceiptFingerprint)
            || owner.physicalSlices == null
            || owner.physicalSlices.Count != 0
            || !string.Equals(batch.batchCommitId, route.batchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(batch.destinationId, route.sourceDestinationId,
                StringComparison.Ordinal)
            || !string.Equals(owner.targetDestinationId,
                route.targetDestinationId, StringComparison.Ordinal)
            || owner.targetPositionX != route.targetPositionX
            || owner.targetPositionY != route.targetPositionY
            || owner.routedQuantity != route.totalQuantity
            || owner.routedMassGrams != route.totalMassGrams
            || route.slices == null
            || route.slices.Count == 0)
        {
            throw new InvalidOperationException(
                $"Physical-pending route '{owner.routeOperationId}' cannot reconstruct its exact Items receipt.");
        }

        ProductionPreparedOutputPhysicalRouteSliceReceipt[] slices = route.slices
            .OrderBy(value => value?.sourceOffsetQuantity ?? -1)
            .ThenBy(value => value?.sourceStackId, StringComparer.Ordinal)
            .ThenBy(value => value?.routedStackId, StringComparer.Ordinal)
            .ThenBy(value => value?.routedOffsetQuantity ?? -1)
            .Select(value => value ?? throw new InvalidOperationException(
                $"Physical-pending route '{owner.routeOperationId}' has a null Items slice."))
            .Select(value =>
            {
                if (!string.Equals(line.outputLineId, value.outputLineId,
                        StringComparison.Ordinal)
                    || !string.Equals(line.lineCommitId, value.lineCommitId,
                        StringComparison.Ordinal)
                    || !string.Equals(line.itemId, value.itemId,
                        StringComparison.Ordinal)
                    || !string.Equals(line.componentFingerprint,
                        value.componentFingerprint, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Physical-pending route '{owner.routeOperationId}' Items slice conflicts with its owner line.");
                }
                return new ProductionPreparedOutputPhysicalRouteSliceReceipt(
                    value.sourceStackId,
                    value.routedStackId,
                    value.outputLineId,
                    value.lineCommitId,
                    value.itemId,
                    value.sourceOffsetQuantity,
                    value.routedOffsetQuantity,
                    value.routedQuantity,
                    value.routedMassGrams,
                    value.componentFingerprint);
            })
            .ToArray();
        ProductionPreparedOutputPhysicalRouteReceipt receipt = new(
            route.routeOperationId,
            route.requestFingerprint,
            route.physicalReceiptFingerprint,
            route.batchCommitId,
            route.sourceDestinationId,
            route.targetDestinationId,
            route.targetPositionX,
            route.targetPositionY,
            route.totalQuantity,
            route.totalMassGrams,
            slices);
        if (!string.Equals(
                ProductionPreparedOutputRoutingAuthority
                    .ComputePhysicalReceiptFingerprint(receipt),
                receipt.PhysicalReceiptFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Physical-pending route '{owner.routeOperationId}' Items receipt fingerprint is invalid.");
        }
        return receipt;
    }

    private static void ValidateLineCoverage(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputRoutingLineSaveData line)
    {
        if (line?.routeOperations == null)
            throw new InvalidOperationException(
                $"Prepared-output routing line '{line?.lineCommitId}' has no route-operation list.");
        ProductionPreparedOutputRouteOperationSaveData[] ordered =
            line.routeOperations
                .OrderBy(value => value?.sourceOffsetQuantity ?? -1)
                .ThenBy(value => value?.routeOperationId,
                    StringComparer.Ordinal)
                .ToArray();
        if (ordered.Any(value => value == null))
            throw new InvalidOperationException(
                $"Prepared-output routing line '{line.lineCommitId}' has a null route owner.");

        int nextQuantity = 0;
        long routedMass = 0L;
        int pendingCount = 0;
        for (int index = 0; index < ordered.Length; index++)
        {
            ProductionPreparedOutputRouteOperationSaveData operation = ordered[index];
            if (operation.sourceOffsetQuantity != nextQuantity)
                throw new InvalidOperationException(
                    $"Prepared-output routing line '{line.lineCommitId}' has an overlapping or gapped source range.");
            if (operation.phase ==
                ProductionPreparedOutputRoutePhase.PhysicalPending)
            {
                pendingCount++;
                if (index != ordered.Length - 1)
                    throw new InvalidOperationException(
                        $"Prepared-output routing line '{line.lineCommitId}' has a non-terminal physical-pending route.");
                continue;
            }
            nextQuantity = checked(nextQuantity + operation.routedQuantity);
            routedMass = checked(routedMass + operation.routedMassGrams);
        }
        if (pendingCount > 1
            || nextQuantity != line.routedQuantity
            || routedMass != line.routedMassGrams
            || checked(line.remainingQuantity + line.routedQuantity)
                != line.originalQuantity
            || checked(line.remainingMassGrams + line.routedMassGrams)
                != line.originalMassGrams)
        {
            throw new InvalidOperationException(
                $"Prepared-output routing line '{batch?.batchCommitId}:{line.lineCommitId}' has incomplete route coverage.");
        }
    }

    private static void ValidateExactRoute(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputRoutingLineSaveData line,
        ProductionPreparedOutputRouteOperationSaveData owner,
        FacilityOutputExactRouteOutboxSaveData route)
    {
        if (!string.Equals(owner.routeOperationId, route.routeOperationId,
                StringComparison.Ordinal)
            || !string.Equals(owner.requestFingerprint,
                route.requestFingerprint, StringComparison.Ordinal)
            || !string.Equals(owner.physicalReceiptFingerprint,
                route.physicalReceiptFingerprint, StringComparison.Ordinal)
            || !string.Equals(batch.batchCommitId, route.batchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(batch.destinationId, route.sourceDestinationId,
                StringComparison.Ordinal)
            || !string.Equals(owner.targetDestinationId,
                route.targetDestinationId, StringComparison.Ordinal)
            || owner.targetPositionX != route.targetPositionX
            || owner.targetPositionY != route.targetPositionY
            || owner.routedQuantity != route.totalQuantity
            || owner.routedMassGrams != route.totalMassGrams)
        {
            throw new InvalidOperationException(
                $"Route '{owner.routeOperationId}' header conflicts across Economy and Items.");
        }

        ProductionPreparedOutputPhysicalRouteSliceSaveData[] expected =
            (owner.physicalSlices
                ?? new List<ProductionPreparedOutputPhysicalRouteSliceSaveData>())
            .OrderBy(value => value.sourceOffsetQuantity)
            .ThenBy(value => value.sourceStackId, StringComparer.Ordinal)
            .ThenBy(value => value.routedStackId, StringComparer.Ordinal)
            .ThenBy(value => value.routedOffsetQuantity)
            .ToArray();
        FacilityOutputExactRouteSliceSaveData[] actual = (route.slices
                ?? new List<FacilityOutputExactRouteSliceSaveData>())
            .OrderBy(value => value.sourceOffsetQuantity)
            .ThenBy(value => value.sourceStackId, StringComparer.Ordinal)
            .ThenBy(value => value.routedStackId, StringComparer.Ordinal)
            .ThenBy(value => value.routedOffsetQuantity)
            .ToArray();
        if (expected.Length == 0 || expected.Length != actual.Length)
            throw new InvalidOperationException(
                $"Route '{owner.routeOperationId}' has missing or extra physical slices.");
        for (int index = 0; index < expected.Length; index++)
        {
            ProductionPreparedOutputPhysicalRouteSliceSaveData left =
                expected[index];
            FacilityOutputExactRouteSliceSaveData right = actual[index];
            if (left == null || right == null
                || !string.Equals(left.sourceStackId, right.sourceStackId,
                    StringComparison.Ordinal)
                || !string.Equals(left.routedStackId, right.routedStackId,
                    StringComparison.Ordinal)
                || left.sourceOffsetQuantity != right.sourceOffsetQuantity
                || left.routedOffsetQuantity != right.routedOffsetQuantity
                || left.routedQuantity != right.routedQuantity
                || left.routedMassGrams != right.routedMassGrams
                || !string.Equals(line.outputLineId, right.outputLineId,
                    StringComparison.Ordinal)
                || !string.Equals(line.lineCommitId, right.lineCommitId,
                    StringComparison.Ordinal)
                || !string.Equals(line.itemId, right.itemId,
                    StringComparison.Ordinal)
                || !string.Equals(line.componentFingerprint,
                    right.componentFingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Route '{owner.routeOperationId}' physical slice {index} conflicts across Economy and Items.");
            }
        }
    }
}
