using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using System.Text;

/// <summary>
/// Items-owned durable evidence for one destructive routing-batch drain. It
/// never routes, drops, releases, or garbage-collects gameplay authority by
/// itself; the upper participant records each successfully committed step.
/// </summary>
public sealed class ProductionCapacityRoutingDrainOutbox :
    IProductionCapacityRoutingDrainOutbox,
    IProductionCapacityRoutingDrainQuery,
    IProductionCapacityRoutingDrainCheckpointGcOutbox
{
    private readonly WorldItemRepository repository;
    private CheckpointGcCandidate activeCheckpointGcCandidate;

    public ProductionCapacityRoutingDrainOutbox(WorldItemRepository repository)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public ProductionCapacityRoutingDrainResult TryPrepare(
        ProductionCapacityRoutingDrainRequest request)
    {
        if (!IsValid(request, out string failure))
            return Conflict(failure);
        if (repository.TryGetPendingCapacityRoutingDrain(
                request.StepOperationId,
                out ProductionCapacityRoutingDrainSaveData existing))
        {
            return Matches(existing, request)
                ? Current(existing, ProductionCapacityRoutingDrainStatus.Replay)
                : Conflict("production-capacity-routing-drain-request-conflict");
        }
        if (repository.TryGetPendingCapacityRoutingDrainForBatch(
                request.BatchCommitId,
                out _))
        {
            return Conflict("production-capacity-routing-drain-batch-owned");
        }

        ProductionCapacityRoutingDrainSaveData prepared = new()
        {
            stepOperationId = request.StepOperationId,
            ownerStableId = request.OwnerStableId,
            facilityId = request.FacilityId,
            sourceDestinationId = request.SourceDestinationId,
            batchCommitId = request.BatchCommitId,
            sourceOutcomeFingerprint = request.SourceOutcomeFingerprint,
            sourceRoutingFingerprint = request.SourceRoutingFingerprint,
            sourceOwnershipFingerprint = request.SourceOwnershipFingerprint,
            requestFingerprint = request.RequestFingerprint,
            phase = ProductionCapacityRoutingDrainPhase.Prepared,
            sourceLines = request.SourceLines.Select(value => value.Clone()).ToList(),
            sourceRoutes = request.SourceRoutes.Select(value => value.Clone()).ToList(),
            sourceSlices = request.SourceSlices.Select(value => value.Clone()).ToList(),
            sourceActorCarries = request.SourceActorCarries
                .Select(value => value.Clone()).ToList(),
            sourceCustodyStackIds = request.SourceCustodyStackIds.ToList(),
            inputQuantity = request.InputQuantity,
            inputMassGrams = request.InputMassGrams
        };
        repository.SetPendingCapacityRoutingDrain(prepared);
        return Current(prepared, ProductionCapacityRoutingDrainStatus.Applied);
    }

    public ProductionCapacityRoutingDrainResult TryBeginRouting(
        string stepOperationId,
        string requestFingerprint)
    {
        if (!TryGet(stepOperationId, out ProductionCapacityRoutingDrainSaveData value))
            return Conflict("production-capacity-routing-drain-missing");
        if (!string.Equals(
                value.requestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict("production-capacity-routing-drain-request-conflict");
        }
        return AdvancePhase(
            value,
            ProductionCapacityRoutingDrainPhase.Prepared,
            ProductionCapacityRoutingDrainPhase.RoutingRemainder,
            static _ => true,
            string.Empty);
    }

    public ProductionCapacityRoutingDrainResult TryRecordLineRouted(
        string stepOperationId,
        string lineCommitId) => RecordProgress(
            stepOperationId,
            lineCommitId,
            value => value.completedLineCommitIds,
            value => value.sourceLines.Select(line => line.lineCommitId).ToList(),
            ProductionCapacityRoutingDrainPhase.RoutingRemainder,
            "line");

    public ProductionCapacityRoutingDrainResult TryBeginQuiescingActors(
        string stepOperationId,
        IEnumerable<string> finalRouteOperationIds,
        IEnumerable<string> preservedStackIds)
    {
        if (!TryGet(stepOperationId, out ProductionCapacityRoutingDrainSaveData value))
            return Conflict("production-capacity-routing-drain-missing");
        string[] routes = Canonical(finalRouteOperationIds);
        string[] stacks = Canonical(preservedStackIds);
        if (!IsCanonicalUnique(routes, requireNonEmpty: true)
            || !IsCanonicalUnique(stacks, requireNonEmpty: true))
        {
            return Conflict("production-capacity-routing-drain-terminal-vector-invalid");
        }
        if (value.phase >= ProductionCapacityRoutingDrainPhase.QuiescingActors)
        {
            return value.finalRouteOperationIds.SequenceEqual(
                        routes,
                        StringComparer.Ordinal)
                    && value.preservedStackIds.SequenceEqual(
                        stacks,
                        StringComparer.Ordinal)
                ? Current(value, ProductionCapacityRoutingDrainStatus.Replay)
                : Conflict("production-capacity-routing-drain-terminal-vector-conflict");
        }
        if (value.phase != ProductionCapacityRoutingDrainPhase.RoutingRemainder
            || !value.completedLineCommitIds.SequenceEqual(
                value.sourceLines.Select(line => line.lineCommitId),
                StringComparer.Ordinal))
        {
            return Deferred("production-capacity-routing-drain-lines-incomplete");
        }
        value.finalRouteOperationIds = routes.ToList();
        value.preservedStackIds = stacks.ToList();
        value.phase = ProductionCapacityRoutingDrainPhase.QuiescingActors;
        repository.SetPendingCapacityRoutingDrain(value);
        return Current(value, ProductionCapacityRoutingDrainStatus.Applied);
    }

    public ProductionCapacityRoutingDrainResult TryConfirmActorQuiesced(
        string stepOperationId,
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt)
    {
        if (!TryGet(stepOperationId, out ProductionCapacityRoutingDrainSaveData value))
            return Conflict("production-capacity-routing-drain-missing");
        if (receipt == null
            || !IsToken(receipt.actorPersistentId)
            || !string.Equals(
                receipt.batchCommitId,
                value.batchCommitId,
                StringComparison.Ordinal)
            || !IsCanonicalUnique(receipt.carriedRowKeys, requireNonEmpty: true)
            || !IsCanonicalUnique(receipt.quantityLeaseIds,
                requireNonEmpty: true)
            || !IsCanonicalUnique(receipt.warehouseAdmissionTokenIds,
                requireNonEmpty: false)
            || !IsDigest(receipt.activePlanFingerprint)
            || !IsDigest(receipt.prePhysicalFingerprint)
            || !IsDigest(receipt.postPhysicalFingerprint)
            || !IsDigest(receipt.receiptFingerprint)
            || !string.Equals(
                receipt.receiptFingerprint,
                ProductionCapacityRoutingDrainFingerprint
                    .CreateActorQuiesceReceiptFingerprint(
                        value.stepOperationId,
                        value.requestFingerprint,
                        receipt),
                StringComparison.Ordinal))
        {
            return Conflict(
                "production-capacity-routing-drain-actor-receipt-invalid");
        }

        string[] expectedRows = value.sourceActorCarries
            .Where(carry => string.Equals(
                carry.actorPersistentId,
                receipt.actorPersistentId,
                StringComparison.Ordinal))
            .Select(ProductionCapacityRoutingDrainFingerprint.ActorCarryKey)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (expectedRows.Length == 0
            || !receipt.carriedRowKeys.SequenceEqual(
                expectedRows,
                StringComparer.Ordinal))
        {
            return Conflict(
                "production-capacity-routing-drain-actor-row-vector-conflict");
        }

        ProductionCapacityRoutingActorQuiesceReceiptSaveData existing =
            value.actorQuiesceReceipts.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.actorPersistentId,
                    receipt.actorPersistentId,
                    StringComparison.Ordinal));
        if (existing != null)
        {
            return string.Equals(
                    existing.receiptFingerprint,
                    receipt.receiptFingerprint,
                    StringComparison.Ordinal)
                ? Current(value, ProductionCapacityRoutingDrainStatus.Replay)
                : Conflict(
                    "production-capacity-routing-drain-actor-receipt-conflict");
        }
        return Deferred(
            "production-capacity-routing-drain-actor-physical-receipt-not-published");
    }

    public ProductionCapacityRoutingDrainResult
        TryBeginReleasingOperationAuthority(string stepOperationId)
    {
        if (!TryGet(stepOperationId, out ProductionCapacityRoutingDrainSaveData value))
            return Conflict("production-capacity-routing-drain-missing");
        string[] actors = value.sourceActorCarries
            .Select(carry => carry.actorPersistentId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToArray();
        return AdvancePhase(
            value,
            ProductionCapacityRoutingDrainPhase.QuiescingActors,
            ProductionCapacityRoutingDrainPhase.ReleasingOperationAuthority,
            current => current.actorQuiesceReceipts
                .Select(receipt => receipt.actorPersistentId)
                .SequenceEqual(
                actors,
                StringComparer.Ordinal),
            "production-capacity-routing-drain-actors-incomplete");
    }

    public ProductionCapacityRoutingDrainResult TryPrepareActorAuthorityRelease(
        string stepOperationId,
        string requestFingerprint,
        ProductionCapacityRoutingActorAuthorityReleaseSaveData plan)
    {
        if (!TryGet(stepOperationId, out ProductionCapacityRoutingDrainSaveData value))
            return Conflict("production-capacity-routing-drain-missing");
        if (value.phase !=
                ProductionCapacityRoutingDrainPhase.ReleasingOperationAuthority)
        {
            return Deferred("production-capacity-routing-drain-phase-mismatch");
        }
        if (!string.Equals(
                value.requestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict("production-capacity-routing-drain-request-conflict");
        }
        if (!TryValidateActorAuthorityReleasePlan(
                value,
                plan,
                out string failure))
        {
            return Conflict(failure);
        }

        ProductionCapacityRoutingActorAuthorityReleaseSaveData existing =
            value.actorAuthorityReleases.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.actorPersistentId,
                    plan.actorPersistentId,
                    StringComparison.Ordinal));
        if (existing != null)
        {
            return string.Equals(
                    existing.planFingerprint,
                    plan.planFingerprint,
                    StringComparison.Ordinal)
                ? Current(value, ProductionCapacityRoutingDrainStatus.Replay)
                : Conflict(
                    "production-capacity-routing-actor-authority-plan-conflict");
        }

        string nextActor = value.sourceActorCarries
            .Select(carry => carry.actorPersistentId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(actorId => actorId, StringComparer.Ordinal)
            .FirstOrDefault(actorId => !value.actorAuthorityReleases.Any(release =>
                release != null
                && string.Equals(
                    release.actorPersistentId,
                    actorId,
                    StringComparison.Ordinal)));
        if (!string.Equals(
                nextActor,
                plan.actorPersistentId,
                StringComparison.Ordinal))
        {
            return Conflict(
                "production-capacity-routing-actor-authority-plan-out-of-order");
        }
        if (value.actorAuthorityReleases.Any(release =>
                release != null && !release.effectsCommitted))
        {
            return Deferred(
                "production-capacity-routing-actor-authority-plan-pending");
        }

        value.actorAuthorityReleases.Add(plan.Clone());
        value.actorAuthorityReleases = value.actorAuthorityReleases
            .OrderBy(release => release.actorPersistentId, StringComparer.Ordinal)
            .ToList();
        repository.SetPendingCapacityRoutingDrain(value);
        return Current(value, ProductionCapacityRoutingDrainStatus.Applied);
    }

    public ProductionCapacityRoutingDrainResult TryCommitActorAuthorityRelease(
        string stepOperationId,
        string planFingerprint,
        string effectFingerprint,
        bool actorPlanFinalized)
    {
        if (!TryGet(stepOperationId, out ProductionCapacityRoutingDrainSaveData value))
            return Conflict("production-capacity-routing-drain-missing");
        if (value.phase !=
                ProductionCapacityRoutingDrainPhase.ReleasingOperationAuthority)
        {
            return Deferred("production-capacity-routing-drain-phase-mismatch");
        }
        ProductionCapacityRoutingActorAuthorityReleaseSaveData plan =
            value.actorAuthorityReleases.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.planFingerprint,
                    planFingerprint,
                    StringComparison.Ordinal));
        if (plan == null)
        {
            return Conflict(
                "production-capacity-routing-actor-authority-plan-missing");
        }
        string expectedEffect = ProductionCapacityRoutingDrainFingerprint
            .CreateActorAuthorityReleaseEffectFingerprint(
                plan.planFingerprint,
                actorPlanFinalized);
        string expectedReceipt = ProductionCapacityRoutingDrainFingerprint
            .CreateActorAuthorityReleaseReceiptFingerprint(
                plan.planFingerprint,
                expectedEffect);
        if (!actorPlanFinalized
            || !IsDigest(effectFingerprint)
            || !string.Equals(
                effectFingerprint,
                expectedEffect,
                StringComparison.Ordinal))
        {
            return Conflict(
                "production-capacity-routing-actor-authority-effect-invalid");
        }
        if (plan.effectsCommitted)
        {
            return plan.actorPlanFinalized
                   && string.Equals(
                       plan.effectFingerprint,
                       expectedEffect,
                       StringComparison.Ordinal)
                   && string.Equals(
                       plan.receiptFingerprint,
                       expectedReceipt,
                       StringComparison.Ordinal)
                ? Current(value, ProductionCapacityRoutingDrainStatus.Replay)
                : Conflict(
                    "production-capacity-routing-actor-authority-effect-conflict");
        }

        plan.effectsCommitted = true;
        plan.actorPlanFinalized = true;
        plan.effectFingerprint = expectedEffect;
        plan.receiptFingerprint = expectedReceipt;
        value.releasedHaulIntentOperationIds = value.actorAuthorityReleases
            .Where(release => release != null && release.effectsCommitted)
            .SelectMany(release => release.operationIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(operationId => operationId, StringComparer.Ordinal)
            .ToList();
        repository.SetPendingCapacityRoutingDrain(value);
        return Current(value, ProductionCapacityRoutingDrainStatus.Applied);
    }

    public ProductionCapacityRoutingDrainResult
        TryBeginAwaitingStablePhysicalState(string stepOperationId)
    {
        if (!TryGet(stepOperationId, out ProductionCapacityRoutingDrainSaveData value))
            return Conflict("production-capacity-routing-drain-missing");
        string[] intents = value.sourceActorCarries
            .Select(carry => carry.haulIntentOperationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToArray();
        return AdvancePhase(
            value,
            ProductionCapacityRoutingDrainPhase.ReleasingOperationAuthority,
            ProductionCapacityRoutingDrainPhase.AwaitingStablePhysicalState,
            current => current.actorAuthorityReleases.All(release =>
                    release != null
                    && release.effectsCommitted
                    && release.actorPlanFinalized)
                && current.releasedHaulIntentOperationIds.SequenceEqual(
                    intents,
                    StringComparer.Ordinal),
            "production-capacity-routing-drain-intents-incomplete");
    }

    public ProductionCapacityRoutingDrainResult TryRecordStablePhysicalStack(
        string stepOperationId,
        string stackId) => RecordProgress(
            stepOperationId,
            stackId,
            value => value.stablePhysicalStackIds,
            value => value.preservedStackIds,
            ProductionCapacityRoutingDrainPhase.AwaitingStablePhysicalState,
            "stable-stack");

    public ProductionCapacityRoutingDrainResult
        TryBeginAwaitingDurableCheckpointGc(string stepOperationId)
    {
        if (!TryGet(stepOperationId, out ProductionCapacityRoutingDrainSaveData value))
            return Conflict("production-capacity-routing-drain-missing");
        return AdvancePhase(
            value,
            ProductionCapacityRoutingDrainPhase.AwaitingStablePhysicalState,
            ProductionCapacityRoutingDrainPhase.AwaitingDurableCheckpointGc,
            current => current.stablePhysicalStackIds.SequenceEqual(
                current.preservedStackIds,
                StringComparer.Ordinal),
            "production-capacity-routing-drain-physical-state-unstable");
    }

    public ProductionCapacityRoutingDrainResult TryCommitEffect(
        string stepOperationId,
        string observedRemovedBatchCommitId,
        int preservedQuantity,
        long preservedMassGrams,
        string resultFingerprint)
    {
        if (!TryGet(stepOperationId, out ProductionCapacityRoutingDrainSaveData value))
            return Conflict("production-capacity-routing-drain-missing");
        if (value.phase is ProductionCapacityRoutingDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or ProductionCapacityRoutingDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
        {
            return string.Equals(
                        value.observedRemovedBatchCommitId,
                        observedRemovedBatchCommitId,
                        StringComparison.Ordinal)
                    && value.preservedQuantity == preservedQuantity
                    && value.preservedMassGrams == preservedMassGrams
                    && string.Equals(
                        value.resultFingerprint,
                        resultFingerprint,
                        StringComparison.Ordinal)
                ? Current(value, ProductionCapacityRoutingDrainStatus.Replay)
                : Conflict("production-capacity-routing-drain-result-conflict");
        }
        if (value.phase != ProductionCapacityRoutingDrainPhase
                .AwaitingDurableCheckpointGc)
        {
            return Deferred(
                "production-capacity-routing-drain-checkpoint-not-awaited");
        }
        if (!string.Equals(
                observedRemovedBatchCommitId,
                value.batchCommitId,
                StringComparison.Ordinal)
            || preservedQuantity != value.inputQuantity
            || preservedMassGrams != value.inputMassGrams
            || !IsDigest(resultFingerprint))
        {
            return Conflict("production-capacity-routing-drain-effect-invalid");
        }
        value.observedRemovedBatchCommitId = observedRemovedBatchCommitId;
        value.preservedQuantity = preservedQuantity;
        value.preservedMassGrams = preservedMassGrams;
        value.resultFingerprint = resultFingerprint;
        value.commitId = ProductionCapacityRoutingDrainFingerprint
            .CreateCommitId(value.stepOperationId, value.requestFingerprint);
        value.receiptFingerprint = ProductionCapacityRoutingDrainFingerprint
            .CreateReceipt(value);
        value.phase = ProductionCapacityRoutingDrainPhase
            .EffectCommittedAwaitingOwnerAck;
        repository.SetPendingCapacityRoutingDrain(value);
        return Current(value, ProductionCapacityRoutingDrainStatus.Applied);
    }

    public ProductionCapacityRoutingDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint)
    {
        if (!TryGet(stepOperationId, out ProductionCapacityRoutingDrainSaveData value))
            return Conflict("production-capacity-routing-drain-missing");
        if (!string.Equals(
                value.receiptFingerprint,
                receiptFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict("production-capacity-routing-drain-receipt-conflict");
        }
        if (value.phase == ProductionCapacityRoutingDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
            return Current(value, ProductionCapacityRoutingDrainStatus.Replay);
        if (value.phase != ProductionCapacityRoutingDrainPhase
                .EffectCommittedAwaitingOwnerAck)
        {
            return Deferred("production-capacity-routing-drain-effect-not-committed");
        }
        value.phase = ProductionCapacityRoutingDrainPhase
            .OwnerAcknowledgedAwaitingCheckpointGc;
        repository.SetPendingCapacityRoutingDrain(value);
        return Current(value, ProductionCapacityRoutingDrainStatus.Applied);
    }

    public ProductionCapacityRoutingDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint)
    {
        if (activeCheckpointGcCandidate != null)
        {
            return Deferred(
                "production-capacity-routing-checkpoint-gc-transaction-active");
        }
        if (!TryGet(stepOperationId, out ProductionCapacityRoutingDrainSaveData value))
        {
            return new ProductionCapacityRoutingDrainResult(
                ProductionCapacityRoutingDrainStatus.Replay,
                string.Empty,
                receiptFingerprint,
                string.Empty);
        }
        if (value.phase != ProductionCapacityRoutingDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
        {
            return Deferred("production-capacity-routing-drain-not-acknowledged");
        }
        if (!string.Equals(
                value.receiptFingerprint,
                receiptFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict("production-capacity-routing-drain-receipt-conflict");
        }
        repository.RemovePendingCapacityRoutingDrain(stepOperationId);
        return Current(value, ProductionCapacityRoutingDrainStatus.Applied);
    }

    public bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<ProductionCapacityRoutingDrainSaveData> records,
        out IProductionCapacityRoutingDrainCheckpointGcCandidate candidate,
        out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (activeCheckpointGcCandidate != null)
        {
            failureReason =
                "production-capacity-routing-checkpoint-gc-already-prepared";
            return false;
        }
        ProductionCapacityRoutingDrainSaveData[] expected = (records
                ?? Array.Empty<ProductionCapacityRoutingDrainSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
            .ToArray();
        if (expected.Any(value => value == null)
            || expected.Select(value => value.stepOperationId)
                .Distinct(StringComparer.Ordinal).Count() != expected.Length
            || expected.Select(value => value.batchCommitId)
                .Distinct(StringComparer.Ordinal).Count() != expected.Length)
        {
            failureReason =
                "production-capacity-routing-checkpoint-gc-records-invalid";
            return false;
        }
        foreach (ProductionCapacityRoutingDrainSaveData row in expected)
        {
            if (row.phase != ProductionCapacityRoutingDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc
                || !IsDigest(row.receiptFingerprint)
                || !string.Equals(
                    row.receiptFingerprint,
                    ProductionCapacityRoutingDrainFingerprint.CreateReceipt(row),
                    StringComparison.Ordinal)
                || !TryGet(row.stepOperationId,
                    out ProductionCapacityRoutingDrainSaveData current)
                || !RowsEqual(current, row))
            {
                failureReason =
                    "production-capacity-routing-checkpoint-gc-row-conflict";
                return false;
            }
        }
        activeCheckpointGcCandidate = new CheckpointGcCandidate(expected);
        candidate = activeCheckpointGcCandidate;
        return true;
    }

    public bool TryPublishCheckpointGarbageCollection(
        IProductionCapacityRoutingDrainCheckpointGcCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.Published)
            return true;
        foreach (ProductionCapacityRoutingDrainSaveData row in exact.ExpectedRows)
        {
            if (!repository
                    .TryRemoveExactPendingCapacityRoutingDrainForCheckpointGc(
                        row,
                        out ProductionCapacityRoutingDrainSaveData removed))
            {
                if (exact.RemovedRows.Any(value => !repository
                        .CanRestoreExactPendingCapacityRoutingDrainForCheckpointGc(
                            value)))
                {
                    throw new InvalidOperationException(
                        "production-capacity-routing-checkpoint-gc-publish-rollback-conflict");
                }
                for (int index = exact.RemovedRows.Count - 1; index >= 0; index--)
                {
                    if (!repository
                            .TryRestoreExactPendingCapacityRoutingDrainForCheckpointGc(
                                exact.RemovedRows[index]))
                    {
                        throw new InvalidOperationException(
                            "production-capacity-routing-checkpoint-gc-publish-rollback-conflict");
                    }
                }
                exact.RemovedRows.Clear();
                failureReason =
                    "production-capacity-routing-checkpoint-gc-publish-conflict";
                return false;
            }
            exact.RemovedRows.Add(removed);
        }
        exact.Published = true;
        return true;
    }

    public void RollbackCheckpointGarbageCollection(
        IProductionCapacityRoutingDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.RemovedRows.Any(value => !repository
                .CanRestoreExactPendingCapacityRoutingDrainForCheckpointGc(
                    value)))
        {
            throw new InvalidOperationException(
                "production-capacity-routing-checkpoint-gc-rollback-conflict");
        }
        for (int index = exact.RemovedRows.Count - 1; index >= 0; index--)
        {
            if (!repository
                    .TryRestoreExactPendingCapacityRoutingDrainForCheckpointGc(
                        exact.RemovedRows[index]))
            {
                throw new InvalidOperationException(
                    "production-capacity-routing-checkpoint-gc-rollback-conflict");
            }
        }
        exact.RemovedRows.Clear();
        exact.Published = false;
    }

    public void CompleteCheckpointGarbageCollection(
        IProductionCapacityRoutingDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (!exact.Published && exact.RemovedRows.Count != 0)
        {
            throw new InvalidOperationException(
                "production-capacity-routing-checkpoint-gc-not-rolled-back");
        }
        exact.Completed = true;
        exact.RemovedRows.Clear();
        activeCheckpointGcCandidate = null;
    }

    public bool TryCapture(
        string stepOperationId,
        out ProductionCapacityRoutingDrainSaveData record)
    {
        record = null;
        if (!TryGet(stepOperationId, out ProductionCapacityRoutingDrainSaveData value))
            return false;
        record = value.Clone();
        return true;
    }

    public bool TryCaptureByBatch(
        string batchCommitId,
        out ProductionCapacityRoutingDrainSaveData record)
    {
        record = null;
        if (string.IsNullOrEmpty(batchCommitId)
            || !string.Equals(
                batchCommitId,
                batchCommitId.Trim(),
                StringComparison.Ordinal)
            || !repository.TryGetPendingCapacityRoutingDrainForBatch(
                batchCommitId,
                out ProductionCapacityRoutingDrainSaveData value))
        {
            return false;
        }
        record = value.Clone();
        return true;
    }

    public bool IsBatchPending(string batchCommitId) =>
        !string.IsNullOrEmpty(batchCommitId)
        && string.Equals(
            batchCommitId,
            batchCommitId.Trim(),
            StringComparison.Ordinal)
        && repository.TryGetPendingCapacityRoutingDrainForBatch(
            batchCommitId,
            out _);

#if UNITY_EDITOR
    public void PublishEditorTestActorQuiesceReceipt(
        string stepOperationId,
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt)
    {
        if (!TryGet(stepOperationId, out ProductionCapacityRoutingDrainSaveData value)
            || receipt == null)
        {
            throw new InvalidOperationException(
                "Editor actor quiesce receipt fixture is invalid.");
        }
        value.actorQuiesceReceipts.Add(receipt.Clone());
        repository.SetPendingCapacityRoutingDrain(value);
    }
#endif

    private static bool TryValidateActorAuthorityReleasePlan(
        ProductionCapacityRoutingDrainSaveData drain,
        ProductionCapacityRoutingActorAuthorityReleaseSaveData plan,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (drain == null
            || plan == null
            || !IsToken(plan.actorPersistentId)
            || !IsDigest(plan.actorQuiesceReceiptFingerprint)
            || !IsCanonicalUnique(plan.operationIds, requireNonEmpty: true)
            || plan.operations == null
            || plan.operations.Count != plan.operationIds.Count
            || plan.operations.Any(row => row == null
                || !IsToken(row.operationId)
                || !IsCanonicalUnique(
                    row.quantityLeaseIds,
                    requireNonEmpty: true)
                || !IsCanonicalUnique(
                    row.warehouseAdmissionTokenIds,
                    requireNonEmpty: false)
                || !IsDigest(row.haulIntentFingerprint))
            || !plan.operations.Select(row => row.operationId)
                .SequenceEqual(plan.operationIds, StringComparer.Ordinal)
            || !IsDigest(plan.activePlanFingerprint)
            || !IsDigest(plan.planFingerprint)
            || plan.effectsCommitted
            || plan.actorPlanFinalized
            || !string.IsNullOrEmpty(plan.effectFingerprint)
            || !string.IsNullOrEmpty(plan.receiptFingerprint))
        {
            failureReason =
                "production-capacity-routing-actor-authority-plan-invalid";
            return false;
        }

        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt =
            drain.actorQuiesceReceipts.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.actorPersistentId,
                    plan.actorPersistentId,
                    StringComparison.Ordinal));
        string[] expectedOperations = drain.sourceActorCarries
            .Where(carry => carry != null
                && string.Equals(
                    carry.actorPersistentId,
                    plan.actorPersistentId,
                    StringComparison.Ordinal))
            .Select(carry => carry.haulIntentOperationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(operationId => operationId, StringComparer.Ordinal)
            .ToArray();
        string[] plannedLeaseIds = plan.operations
            .SelectMany(row => row.quantityLeaseIds)
            .OrderBy(leaseId => leaseId, StringComparer.Ordinal)
            .ToArray();
        string[] plannedAdmissionIds = plan.operations
            .SelectMany(row => row.warehouseAdmissionTokenIds)
            .OrderBy(tokenId => tokenId, StringComparer.Ordinal)
            .ToArray();
        if (receipt == null
            || !string.Equals(
                receipt.receiptFingerprint,
                plan.actorQuiesceReceiptFingerprint,
                StringComparison.Ordinal)
            || !plan.operationIds.SequenceEqual(
                expectedOperations,
                StringComparer.Ordinal)
            || !plannedLeaseIds.SequenceEqual(
                receipt.quantityLeaseIds,
                StringComparer.Ordinal)
            || !plannedAdmissionIds.SequenceEqual(
                receipt.warehouseAdmissionTokenIds,
                StringComparer.Ordinal)
            || !string.Equals(
                plan.activePlanFingerprint,
                receipt.activePlanFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                plan.planFingerprint,
                ProductionCapacityRoutingDrainFingerprint
                    .CreateActorAuthorityReleasePlanFingerprint(
                        drain.stepOperationId,
                        drain.requestFingerprint,
                        plan),
                StringComparison.Ordinal))
        {
            failureReason =
                "production-capacity-routing-actor-authority-plan-source-conflict";
            return false;
        }
        return true;
    }

    private CheckpointGcCandidate RequireCheckpointGcCandidate(
        IProductionCapacityRoutingDrainCheckpointGcCandidate candidate)
    {
        if (candidate is not CheckpointGcCandidate exact
            || exact.Completed
            || !ReferenceEquals(exact, activeCheckpointGcCandidate))
        {
            throw new InvalidOperationException(
                "production-capacity-routing-checkpoint-gc-candidate-conflict");
        }
        return exact;
    }

    private static bool RowsEqual(
        ProductionCapacityRoutingDrainSaveData left,
        ProductionCapacityRoutingDrainSaveData right) => left != null
        && right != null
        && string.Equals(
            JsonUtility.ToJson(left),
            JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    private sealed class CheckpointGcCandidate :
        IProductionCapacityRoutingDrainCheckpointGcCandidate
    {
        internal CheckpointGcCandidate(
            IReadOnlyList<ProductionCapacityRoutingDrainSaveData> expectedRows)
        {
            ExpectedRows = expectedRows
                ?? throw new ArgumentNullException(nameof(expectedRows));
        }

        internal IReadOnlyList<ProductionCapacityRoutingDrainSaveData> ExpectedRows
        { get; }
        internal List<ProductionCapacityRoutingDrainSaveData> RemovedRows { get; } =
            new();
        internal bool Published { get; set; }
        internal bool Completed { get; set; }
    }

    private ProductionCapacityRoutingDrainResult RecordProgress(
        string stepOperationId,
        string identity,
        Func<ProductionCapacityRoutingDrainSaveData, List<string>> target,
        Func<ProductionCapacityRoutingDrainSaveData, List<string>> allowed,
        ProductionCapacityRoutingDrainPhase requiredPhase,
        string kind)
    {
        if (!IsToken(identity))
            return Conflict("production-capacity-routing-drain-" + kind + "-invalid");
        if (!TryGet(stepOperationId, out ProductionCapacityRoutingDrainSaveData value))
            return Conflict("production-capacity-routing-drain-missing");
        if (value.phase != requiredPhase)
            return Deferred("production-capacity-routing-drain-phase-mismatch");
        List<string> completed = target(value);
        List<string> planned = allowed(value);
        int index = completed.BinarySearch(identity, StringComparer.Ordinal);
        if (index >= 0)
            return Current(value, ProductionCapacityRoutingDrainStatus.Replay);
        if (completed.Count >= planned.Count
            || !string.Equals(
                planned[completed.Count],
                identity,
                StringComparison.Ordinal))
        {
            return Conflict(
                "production-capacity-routing-drain-" + kind
                + "-out-of-order-or-not-planned");
        }
        completed.Add(identity);
        repository.SetPendingCapacityRoutingDrain(value);
        return Current(value, ProductionCapacityRoutingDrainStatus.Applied);
    }

    private ProductionCapacityRoutingDrainResult AdvancePhase(
        ProductionCapacityRoutingDrainSaveData value,
        ProductionCapacityRoutingDrainPhase expected,
        ProductionCapacityRoutingDrainPhase next,
        Func<ProductionCapacityRoutingDrainSaveData, bool> canAdvance,
        string incompleteReason)
    {
        if (value.phase == next || value.phase > next)
            return Current(value, ProductionCapacityRoutingDrainStatus.Replay);
        if (value.phase != expected)
            return Deferred("production-capacity-routing-drain-phase-mismatch");
        if (!canAdvance(value))
            return Deferred(incompleteReason);
        value.phase = next;
        repository.SetPendingCapacityRoutingDrain(value);
        return Current(value, ProductionCapacityRoutingDrainStatus.Applied);
    }

    private bool TryGet(
        string stepOperationId,
        out ProductionCapacityRoutingDrainSaveData value)
    {
        value = null;
        if (!IsToken(stepOperationId)
            || !repository.TryGetPendingCapacityRoutingDrain(
                stepOperationId,
                out ProductionCapacityRoutingDrainSaveData stored))
        {
            return false;
        }
        value = stored.Clone();
        return true;
    }

    private static bool IsValid(
        ProductionCapacityRoutingDrainRequest request,
        out string failure)
    {
        failure = string.Empty;
        if (request == null
            || !IsToken(request.StepOperationId)
            || !IsToken(request.FacilityId)
            || !IsToken(request.SourceDestinationId)
            || !IsToken(request.BatchCommitId)
            || !string.Equals(
                request.OwnerStableId,
                "routing-batch:" + request.BatchCommitId,
                StringComparison.Ordinal)
            || !IsDigest(request.SourceOutcomeFingerprint)
            || !IsDigest(request.SourceRoutingFingerprint)
            || !IsDigest(request.SourceOwnershipFingerprint)
            || !IsDigest(request.RequestFingerprint)
            || request.InputQuantity <= 0
            || request.InputMassGrams <= 0L
            || !ValidateSourceVectors(request)
            || !string.Equals(
                request.RequestFingerprint,
                ProductionCapacityRoutingDrainFingerprint.CreateRequest(
                    request.StepOperationId,
                    request.OwnerStableId,
                    request.FacilityId,
                    request.SourceDestinationId,
                    request.BatchCommitId,
                    request.SourceOutcomeFingerprint,
                    request.SourceRoutingFingerprint,
                    request.SourceOwnershipFingerprint,
                    request.SourceLines,
                    request.SourceRoutes,
                    request.SourceSlices,
                    request.SourceActorCarries,
                    request.SourceCustodyStackIds,
                    request.InputQuantity,
                    request.InputMassGrams),
                StringComparison.Ordinal))
        {
            failure = "production-capacity-routing-drain-request-invalid";
            return false;
        }
        return true;
    }

    private static bool ValidateSourceVectors(
        ProductionCapacityRoutingDrainRequest request)
    {
        if (request.SourceLines == null
            || request.SourceLines.Count == 0
            || request.SourceRoutes == null
            || request.SourceSlices == null
            || request.SourceActorCarries == null
            || !IsCanonicalUnique(
                request.SourceLines.Select(line => line?.lineCommitId).ToArray(),
                requireNonEmpty: true)
            || !IsCanonicalUnique(
                request.SourceRoutes.Select(route => route?.routeOperationId).ToArray(),
                requireNonEmpty: false)
            || !IsCanonicalUnique(
                request.SourceSlices.Select(
                    ProductionCapacityRoutingDrainFingerprint.SliceKey).ToArray(),
                requireNonEmpty: false)
            || !IsCanonicalUnique(
                request.SourceActorCarries.Select(
                    ProductionCapacityRoutingDrainFingerprint.ActorCarryKey).ToArray(),
                requireNonEmpty: false)
            || !IsCanonicalUnique(request.SourceCustodyStackIds, requireNonEmpty: true))
        {
            return false;
        }

        HashSet<string> routeIds = request.SourceRoutes
            .Select(route => route.routeOperationId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> custodyStackIds = request.SourceCustodyStackIds
            .ToHashSet(StringComparer.Ordinal);
        int quantity = 0;
        long mass = 0L;
        try
        {
            foreach (ProductionCapacityRoutingDrainLineSaveData line in
                     request.SourceLines)
            {
                if (line == null
                    || !IsToken(line.outputLineId)
                    || !IsToken(line.itemId)
                    || !IsDigest(line.componentFingerprint)
                    || !IsToken(line.outputCapabilityId)
                    || line.outputCapabilityVersion <= 0
                    || !IsToken(line.outputComponentCodecId)
                    || line.outputComponentCodecVersion <= 0
                    || !IsDigest(line.outputCapabilityFingerprint)
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
                quantity = checked(quantity + line.originalQuantity);
                mass = checked(mass + line.originalMassGrams);
            }
        }
        catch (OverflowException)
        {
            return false;
        }
        if (quantity != request.InputQuantity || mass != request.InputMassGrams)
            return false;

        if (request.SourceRoutes.Any(route => route == null
                || !IsDigest(route.requestFingerprint)
                || route.phase is < 1 or > 3
                || (route.phase == 1
                    ? !string.IsNullOrEmpty(route.physicalReceiptFingerprint)
                    : !IsDigest(route.physicalReceiptFingerprint))
                || route.currentDeliveryRevision < 0L
                || !IsDigest(route.currentDeliveryRevisionFingerprint)
                || !IsCanonicalText(route.currentTargetDestinationId)
                || !IsCanonicalText(route.currentTargetAuthorityFingerprint)))
        {
            return false;
        }
        if (request.SourceSlices.Any(slice => slice == null
                || !routeIds.Contains(slice.routeOperationId)
                || !custodyStackIds.Contains(slice.routedStackId)
                || !IsToken(slice.sourceStackId)
                || !IsToken(slice.outputLineId)
                || !IsToken(slice.lineCommitId)
                || !IsToken(slice.itemId)
                || slice.sourceOffsetQuantity < 0
                || slice.routedOffsetQuantity < 0
                || slice.routedQuantity <= 0
                || slice.routedMassGrams <= 0L
                || !IsDigest(slice.componentFingerprint)))
        {
            return false;
        }
        return request.SourceActorCarries.All(carry => carry != null
            && IsToken(carry.actorPersistentId)
            && IsToken(carry.haulIntentOperationId)
            && routeIds.Contains(carry.routeOperationId)
            && custodyStackIds.Contains(carry.carriedStackId)
            && IsToken(carry.sourceStackId)
            && carry.quantity > 0
            && carry.massGrams > 0L
            && IsDigest(carry.stackSignature));
    }

    private static bool Matches(
        ProductionCapacityRoutingDrainSaveData value,
        ProductionCapacityRoutingDrainRequest request) =>
        string.Equals(value.ownerStableId, request.OwnerStableId, StringComparison.Ordinal)
        && string.Equals(value.facilityId, request.FacilityId, StringComparison.Ordinal)
        && string.Equals(value.sourceDestinationId, request.SourceDestinationId,
            StringComparison.Ordinal)
        && string.Equals(value.batchCommitId, request.BatchCommitId,
            StringComparison.Ordinal)
        && string.Equals(value.requestFingerprint, request.RequestFingerprint,
            StringComparison.Ordinal)
        && string.Equals(value.sourceOutcomeFingerprint,
            request.SourceOutcomeFingerprint, StringComparison.Ordinal)
        && string.Equals(value.sourceRoutingFingerprint,
            request.SourceRoutingFingerprint, StringComparison.Ordinal)
        && string.Equals(value.sourceOwnershipFingerprint,
            request.SourceOwnershipFingerprint, StringComparison.Ordinal)
        && value.inputQuantity == request.InputQuantity
        && value.inputMassGrams == request.InputMassGrams
        && string.Equals(
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
            request.RequestFingerprint,
            StringComparison.Ordinal);

    private static string[] Canonical(IEnumerable<string> source) =>
        (source ?? Array.Empty<string>())
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static bool IsCanonicalUnique(
        IReadOnlyList<string> values,
        bool requireNonEmpty)
    {
        if (values == null || requireNonEmpty && values.Count == 0)
            return false;
        for (int index = 0; index < values.Count; index++)
        {
            if (!IsToken(values[index])
                || index > 0
                    && string.CompareOrdinal(values[index - 1], values[index]) >= 0)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsToken(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsCanonicalText(string value) => value != null
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsDigest(string value) => value?.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private static ProductionCapacityRoutingDrainResult Current(
        ProductionCapacityRoutingDrainSaveData value,
        ProductionCapacityRoutingDrainStatus status) => new(
            status,
            value.commitId,
            value.receiptFingerprint,
            string.Empty);

    private static ProductionCapacityRoutingDrainResult Deferred(string reason) =>
        new(ProductionCapacityRoutingDrainStatus.Deferred, string.Empty,
            string.Empty, reason);

    private static ProductionCapacityRoutingDrainResult Conflict(string reason) =>
        new(ProductionCapacityRoutingDrainStatus.Conflict, string.Empty,
            string.Empty, reason);
}
