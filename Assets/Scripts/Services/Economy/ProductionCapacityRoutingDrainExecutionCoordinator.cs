using System;
using System.Collections.Generic;
using System.Linq;

public interface IProductionCapacityRoutingDrainExecutionCoordinator
{
    [GameplayInternalOnly(
        "Routes the complete frozen batch through the normal exact lifecycle, atomically quiesces actors, and waits for durable checkpoint GC.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryProgress(
        string stepOperationId,
        string requestFingerprint);
}

public interface IProductionCapacityRoutingCheckpointGcAbsenceQuery
{
    bool TryVerifyRoutingAuthorityAbsent(
        string batchCommitId,
        out string failureReason);
}

/// <summary>
/// Live executor for the capacity-routing producer outbox. All normal route
/// operations and actor quiescence complete synchronously on the Unity main
/// thread so no new actor can enter the frozen source vector between them.
/// The durable checkpoint itself remains asynchronous and is observed on a
/// later Tick before the producer effect receipt is committed.
/// </summary>
public sealed class ProductionCapacityRoutingDrainExecutionCoordinator :
    IProductionCapacityRoutingDrainExecutionCoordinator,
    IProductionCapacityRoutingCheckpointGcAbsenceQuery
{
    private readonly IProductionCapacityRoutingDrainOutbox producer;
    private readonly IProductionPreparedOutputRoutingBatchQuery batches;
    private readonly IProductionPreparedOutputRoutingAuthority routing;
    private readonly IProductionPreparedOutputExactRouteLifecycle lifecycle;
    private readonly IProductionCapacityRoutingPhysicalSourceQuery physical;
    private readonly IProductionCapacityRoutingOperationAuthorityReleaseCoordinator
        actorAuthority;
    private readonly IFacilityOutputExactRouteOutboxQuery exactRoutes;

    public ProductionCapacityRoutingDrainExecutionCoordinator(
        IProductionCapacityRoutingDrainOutbox producer,
        IProductionPreparedOutputRoutingBatchQuery batches,
        IProductionPreparedOutputRoutingAuthority routing,
        IProductionPreparedOutputExactRouteLifecycle lifecycle,
        IProductionCapacityRoutingPhysicalSourceQuery physical,
        IProductionCapacityRoutingOperationAuthorityReleaseCoordinator
            actorAuthority,
        IFacilityOutputExactRouteOutboxQuery exactRoutes)
    {
        this.producer = producer
            ?? throw new ArgumentNullException(nameof(producer));
        this.batches = batches ?? throw new ArgumentNullException(nameof(batches));
        this.routing = routing ?? throw new ArgumentNullException(nameof(routing));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.physical = physical
            ?? throw new ArgumentNullException(nameof(physical));
        this.actorAuthority = actorAuthority
            ?? throw new ArgumentNullException(nameof(actorAuthority));
        this.exactRoutes = exactRoutes
            ?? throw new ArgumentNullException(nameof(exactRoutes));
    }

    [GameplayInternalOnly(
        "Routes the complete frozen batch through the normal exact lifecycle, atomically quiesces actors, and waits for durable checkpoint GC.",
        "Production capacity-routing destructive-drain participant only")]
    public ProductionCapacityRoutingDrainResult TryProgress(
        string stepOperationId,
        string requestFingerprint)
    {
        if (!TryCaptureExact(
                stepOperationId,
                requestFingerprint,
                out ProductionCapacityRoutingDrainSaveData drain,
                out ProductionCapacityRoutingDrainResult failure))
        {
            return failure;
        }

        if (drain.phase == ProductionCapacityRoutingDrainPhase.Prepared)
        {
            ProductionCapacityRoutingDrainResult started = producer.TryBeginRouting(
                stepOperationId,
                requestFingerprint);
            if (IsFailure(started))
                return started;
            if (!TryCaptureExact(
                    stepOperationId,
                    requestFingerprint,
                    out drain,
                    out failure))
            {
                return failure;
            }
        }

        if (drain.phase == ProductionCapacityRoutingDrainPhase.RoutingRemainder)
        {
            ProductionCapacityRoutingDrainResult routed = RouteAndQuiesce(drain);
            if (IsFailure(routed))
                return routed;
            if (!TryCaptureExact(
                    stepOperationId,
                    requestFingerprint,
                    out drain,
                    out failure))
            {
                return failure;
            }
        }

        if (drain.phase is ProductionCapacityRoutingDrainPhase.QuiescingActors
            or ProductionCapacityRoutingDrainPhase.ReleasingOperationAuthority)
        {
            ProductionCapacityRoutingDrainResult actors = actorAuthority
                .TryQuiesceAndReleaseAllActors(
                    stepOperationId,
                    requestFingerprint);
            if (IsFailure(actors))
                return actors;
            if (!TryCaptureExact(
                    stepOperationId,
                    requestFingerprint,
                    out drain,
                    out failure))
            {
                return failure;
            }
        }

        if (drain.phase ==
            ProductionCapacityRoutingDrainPhase.AwaitingStablePhysicalState)
        {
            ProductionCapacityRoutingDrainResult stable =
                VerifyStablePhysicalState(drain);
            if (IsFailure(stable))
                return stable;
            if (!TryCaptureExact(
                    stepOperationId,
                    requestFingerprint,
                    out drain,
                    out failure))
            {
                return failure;
            }
        }

        if (drain.phase ==
            ProductionCapacityRoutingDrainPhase.AwaitingDurableCheckpointGc)
        {
            if (!CheckpointGcCompleted(drain.batchCommitId))
            {
                return Deferred(
                    "production-capacity-routing-checkpoint-gc-pending");
            }
            return producer.TryCommitEffect(
                drain.stepOperationId,
                drain.batchCommitId,
                drain.inputQuantity,
                drain.inputMassGrams,
                ProductionCapacityRoutingDrainFingerprint
                    .CreateResultFingerprint(drain));
        }

        if (drain.phase is ProductionCapacityRoutingDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or ProductionCapacityRoutingDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
        {
            return producer.TryCommitEffect(
                drain.stepOperationId,
                drain.batchCommitId,
                drain.inputQuantity,
                drain.inputMassGrams,
                drain.resultFingerprint);
        }

        return Conflict("production-capacity-routing-phase-unsupported");
    }

    private ProductionCapacityRoutingDrainResult RouteAndQuiesce(
        ProductionCapacityRoutingDrainSaveData drain)
    {
        if (!batches.TryCaptureBatch(
                drain.batchCommitId,
                out ProductionPreparedOutputRoutingBatchSnapshot batch))
        {
            return Conflict("production-capacity-routing-batch-missing-before-gc");
        }
        if (!physical.TryCapture(
                drain.batchCommitId,
                drain.sourceDestinationId,
                out ProductionCapacityRoutingPhysicalSourceSnapshot source,
                out string physicalFailure))
        {
            return Conflict(physicalFailure);
        }
        if (!MatchesFrozenPhysicalSource(
                drain,
                source,
                requireInitialStackIds: true))
        {
            return Conflict(
                "production-capacity-routing-physical-source-drift");
        }

        foreach (ProductionPreparedOutputRouteRequestSnapshot operation in
                 batch.RouteOperations
                     .OrderBy(value => value.RouteOperationId,
                         StringComparer.Ordinal))
        {
            if (HasCompletedDeliveryAuthority(operation))
                continue;
            ProductionPreparedOutputExactRouteLifecycleResult progress =
                lifecycle.TryProgress(operation);
            if (!progress.Completed)
                return FromLifecycle(progress);
        }

        if (!batches.TryCaptureBatch(drain.batchCommitId, out batch))
        {
            return Conflict(
                "production-capacity-routing-batch-disappeared-during-route");
        }
        foreach (ProductionPreparedOutputRoutingLineSnapshot line in batch.Lines
                     .OrderBy(value => value.LineCommitId,
                         StringComparer.Ordinal))
        {
            if (line.RemainingQuantity > 0)
            {
                int exact = lifecycle.ResolveExactQuantity(
                    line,
                    line.RemainingQuantity);
                if (exact != line.RemainingQuantity)
                {
                    return Conflict(
                        "production-capacity-routing-line-quantum-conflict:"
                        + line.LineCommitId);
                }
                ProductionPreparedOutputRouteRequestSnapshot operation =
                    routing.PrepareRoute(
                        line.BatchCommitId,
                        line.LineCommitId,
                        string.Empty,
                        source.OriginPosition.x,
                        source.OriginPosition.y,
                        exact);
                ProductionPreparedOutputExactRouteLifecycleResult progress =
                    lifecycle.TryProgress(operation);
                if (!progress.Completed)
                    return FromLifecycle(progress);
                if (!batches.TryCaptureBatch(drain.batchCommitId, out batch))
                {
                    return Conflict(
                        "production-capacity-routing-batch-disappeared-during-line-route");
                }
            }
        }

        if (!batches.TryCaptureBatch(drain.batchCommitId, out batch)
            || batch.Lines.Any(value => value.RemainingQuantity != 0
                || value.RemainingMassGrams != 0L)
            || batch.RouteOperations.Any(value =>
                !HasCompletedDeliveryAuthority(value)))
        {
            return Deferred(
                "production-capacity-routing-normal-route-incomplete");
        }

        foreach (ProductionPreparedOutputRoutingLineSnapshot line in batch.Lines
                     .OrderBy(value => value.LineCommitId,
                         StringComparer.Ordinal))
        {
            ProductionCapacityRoutingDrainResult recorded =
                producer.TryRecordLineRouted(
                    drain.stepOperationId,
                    line.LineCommitId);
            if (IsFailure(recorded))
                return recorded;
        }

        if (!physical.TryCapture(
                drain.batchCommitId,
                drain.sourceDestinationId,
                out ProductionCapacityRoutingPhysicalSourceSnapshot terminal,
                out physicalFailure)
            || !MatchesFrozenPhysicalSource(
                drain,
                terminal,
                requireInitialStackIds: false))
        {
            return Conflict(
                physicalFailure.Length > 0
                    ? physicalFailure
                    : "production-capacity-routing-terminal-source-drift");
        }

        string[] routes = batch.RouteOperations
            .Select(value => value.RouteOperationId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return producer.TryBeginQuiescingActors(
            drain.stepOperationId,
            routes,
            terminal.CustodyStackIds);
    }

    private ProductionCapacityRoutingDrainResult VerifyStablePhysicalState(
        ProductionCapacityRoutingDrainSaveData drain)
    {
        if (!physical.TryCapture(
                drain.batchCommitId,
                drain.sourceDestinationId,
                out ProductionCapacityRoutingPhysicalSourceSnapshot stable,
                out string failureReason))
        {
            return Conflict(failureReason);
        }
        string[] expected = drain.preservedStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (stable.ActorCarries.Count != 0
            || stable.TotalQuantity != drain.inputQuantity
            || stable.TotalMassGrams != drain.inputMassGrams
            || !stable.CustodyStackIds.SequenceEqual(
                expected,
                StringComparer.Ordinal))
        {
            return Conflict(
                "production-capacity-routing-stable-physical-state-drift");
        }

        foreach (string stackId in expected)
        {
            ProductionCapacityRoutingDrainResult recorded =
                producer.TryRecordStablePhysicalStack(
                    drain.stepOperationId,
                    stackId);
            if (IsFailure(recorded))
                return recorded;
        }
        return producer.TryBeginAwaitingDurableCheckpointGc(
            drain.stepOperationId);
    }

    public bool TryVerifyRoutingAuthorityAbsent(
        string batchCommitId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrEmpty(batchCommitId)
            || !string.Equals(
                batchCommitId,
                batchCommitId.Trim(),
                StringComparison.Ordinal))
        {
            failureReason =
                "production-capacity-routing-checkpoint-gc-batch-invalid";
            return false;
        }
        if (batches.TryCaptureBatch(batchCommitId, out _))
        {
            failureReason =
                "production-capacity-routing-checkpoint-gc-batch-still-live";
            return false;
        }
        if ((exactRoutes.CapturePendingRoutes()
                ?? Array.Empty<FacilityOutputExactRoutePendingSnapshot>())
            .Any(value => string.Equals(
                value?.Receipt?.BatchCommitId,
                batchCommitId,
                StringComparison.Ordinal)))
        {
            failureReason =
                "production-capacity-routing-checkpoint-gc-route-still-live";
            return false;
        }
        return true;
    }

    private bool CheckpointGcCompleted(string batchCommitId) =>
        TryVerifyRoutingAuthorityAbsent(batchCommitId, out _);

    private bool TryCaptureExact(
        string stepOperationId,
        string requestFingerprint,
        out ProductionCapacityRoutingDrainSaveData drain,
        out ProductionCapacityRoutingDrainResult failure)
    {
        if (!producer.TryCapture(stepOperationId, out drain))
        {
            failure = Conflict("production-capacity-routing-drain-missing");
            return false;
        }
        if (!string.Equals(
                drain.requestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal))
        {
            failure = Conflict(
                "production-capacity-routing-drain-request-conflict");
            return false;
        }
        failure = default;
        return true;
    }

    private static bool MatchesFrozenPhysicalSource(
        ProductionCapacityRoutingDrainSaveData drain,
        ProductionCapacityRoutingPhysicalSourceSnapshot source,
        bool requireInitialStackIds)
    {
        string[] expectedStacks = drain.sourceCustodyStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actualStacks = source.CustodyStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expectedCarries = drain.sourceActorCarries
            .Select(ProductionCapacityRoutingDrainFingerprint.ActorCarryKey)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actualCarries = source.ActorCarries
            .Select(ProductionCapacityRoutingDrainFingerprint.ActorCarryKey)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return source.TotalQuantity == drain.inputQuantity
            && source.TotalMassGrams == drain.inputMassGrams
            && (!requireInitialStackIds
                || expectedStacks.SequenceEqual(
                    actualStacks,
                    StringComparer.Ordinal))
            && expectedCarries.SequenceEqual(actualCarries, StringComparer.Ordinal)
            && source.ActorCarries.Zip(
                    drain.sourceActorCarries.OrderBy(
                        ProductionCapacityRoutingDrainFingerprint.ActorCarryKey,
                        StringComparer.Ordinal),
                    (left, right) => left.quantity == right.quantity
                        && left.massGrams == right.massGrams
                        && string.Equals(
                            left.stackSignature,
                            right.stackSignature,
                            StringComparison.Ordinal))
                .All(value => value);
    }

    private static bool HasCompletedDeliveryAuthority(
        ProductionPreparedOutputRouteRequestSnapshot operation) =>
        operation.Phase == ProductionPreparedOutputRoutePhase
            .ItemsAcknowledgedAwaitingCheckpointGc
        && operation.CurrentDeliveryTargetKind !=
            ProductionPreparedOutputDeliveryTargetKind.WarehouseSelectionPending
        && !string.IsNullOrEmpty(operation.CurrentTargetDestinationId)
        && !string.IsNullOrEmpty(operation.CurrentTargetAuthorityFingerprint);

    private static ProductionCapacityRoutingDrainResult FromLifecycle(
        ProductionPreparedOutputExactRouteLifecycleResult result) =>
        result.Status == ProductionPreparedOutputExactRouteLifecycleStatus.Conflict
            ? Conflict(result.Message.Length > 0
                ? result.Message
                : result.Reason.ToString())
            : Deferred(result.Message.Length > 0
                ? result.Message
                : result.Reason.ToString());

    private static bool IsFailure(ProductionCapacityRoutingDrainResult value) =>
        value.Status is ProductionCapacityRoutingDrainStatus.Conflict
            or ProductionCapacityRoutingDrainStatus.Deferred;

    private static ProductionCapacityRoutingDrainResult Deferred(string reason) =>
        new(
            ProductionCapacityRoutingDrainStatus.Deferred,
            string.Empty,
            string.Empty,
            reason);

    private static ProductionCapacityRoutingDrainResult Conflict(string reason) =>
        new(
            ProductionCapacityRoutingDrainStatus.Conflict,
            string.Empty,
            string.Empty,
            reason);
}
