using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Completes the non-saveable capacity-routing transition in one synchronous
/// actor-set transaction. Stable saves may exist before quiescence or after
/// every actor authority receipt commits, never while physical cargo is Loose
/// and its haul authority is still live.
/// </summary>
public sealed class ProductionCapacityRoutingOperationAuthorityReleaseCoordinator :
    IProductionCapacityRoutingOperationAuthorityReleaseCoordinator
{
    private readonly WorldItemRepository repository;
    private readonly IProductionCapacityRoutingDrainOutbox outbox;
    private readonly ICharacterCarryInventoryRegistry inventories;
    private readonly ItemQuantityReservationService reservations;
    private readonly WarehouseMassAdmissionService admissions;
    private readonly IProductionCapacityRoutingActorQuiescence quiescence;
    private readonly IItemReservationMutationGate mutationGate;

#if UNITY_EDITOR
    public Func<string, int, bool> DebugFailBeforeAuthorityRowMutation;
#endif

    public ProductionCapacityRoutingOperationAuthorityReleaseCoordinator(
        WorldItemRepository repository,
        IProductionCapacityRoutingDrainOutbox outbox,
        ICharacterCarryInventoryRegistry inventories,
        ItemQuantityReservationService reservations,
        WarehouseMassAdmissionService admissions,
        IProductionCapacityRoutingActorQuiescence quiescence,
        IItemReservationMutationGate mutationGate)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        this.inventories = inventories
            ?? throw new ArgumentNullException(nameof(inventories));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.admissions = admissions
            ?? throw new ArgumentNullException(nameof(admissions));
        this.quiescence = quiescence
            ?? throw new ArgumentNullException(nameof(quiescence));
        this.mutationGate = mutationGate
            ?? throw new ArgumentNullException(nameof(mutationGate));
    }

    public ProductionCapacityRoutingDrainResult
        TryQuiesceAndReleaseAllActors(
            string stepOperationId,
            string drainRequestFingerprint)
    {
        if (!TryCaptureExact(
                stepOperationId,
                drainRequestFingerprint,
                out ProductionCapacityRoutingDrainSaveData drain,
                out ProductionCapacityRoutingDrainResult captureFailure))
            return captureFailure;
        if (drain.phase >
            ProductionCapacityRoutingDrainPhase.ReleasingOperationAuthority)
            return Current(drain, ProductionCapacityRoutingDrainStatus.Replay);
        if (drain.phase < ProductionCapacityRoutingDrainPhase.QuiescingActors)
        {
            return Deferred(
                "production-capacity-routing-drain-not-ready-for-actor-transition");
        }

        // Normal lease/admission/intent mutations are fenced. The exact
        // prepared-plan adapters below remain the sole permitted mutations.
        // Unity invokes this method synchronously on its main thread, so a
        // successful call exposes no intermediate save boundary.
        using (mutationGate.EnterCaptureBarrier())
        {
            if (drain.phase ==
                ProductionCapacityRoutingDrainPhase.QuiescingActors)
            {
                ProductionCapacityRoutingDrainResult quiesced =
                    TryQuiesceEveryActor(
                        stepOperationId,
                        drainRequestFingerprint,
                        drain);
                if (IsFailure(quiesced))
                    return quiesced;
                if (!TryCaptureExact(
                        stepOperationId,
                        drainRequestFingerprint,
                        out drain,
                        out captureFailure))
                    return captureFailure;
                ProductionCapacityRoutingDrainResult releasing =
                    outbox.TryBeginReleasingOperationAuthority(
                        stepOperationId);
                if (IsFailure(releasing))
                    return releasing;
                if (!TryCaptureExact(
                        stepOperationId,
                        drainRequestFingerprint,
                        out drain,
                        out captureFailure))
                    return captureFailure;
            }

            ProductionCapacityRoutingDrainResult released =
                TryReleaseEveryActor(
                    stepOperationId,
                    drainRequestFingerprint,
                    drain);
            if (IsFailure(released))
                return released;
            return outbox.TryBeginAwaitingStablePhysicalState(
                stepOperationId);
        }
    }

    private ProductionCapacityRoutingDrainResult TryQuiesceEveryActor(
        string stepOperationId,
        string requestFingerprint,
        ProductionCapacityRoutingDrainSaveData drain)
    {
        string[] actorIds = drain.sourceActorCarries
            .Where(value => value != null)
            .Select(value => value.actorPersistentId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        foreach (string actorId in actorIds)
        {
            if (!TryResolveActor(
                    actorId,
                    out CharacterActor actor,
                    out CharacterCarryInventory inventory,
                    out AbilityHaul haul))
            {
                return Deferred(
                    "production-capacity-routing-actor-runtime-unavailable:"
                    + actorId);
            }
            ProductionCapacityRoutingDrainActorCarrySaveData[] carries =
                drain.sourceActorCarries
                    .Where(value => value != null
                        && string.Equals(
                            value.actorPersistentId,
                            actorId,
                            StringComparison.Ordinal))
                    .OrderBy(value => value.carriedStackId,
                        StringComparer.Ordinal)
                    .ThenBy(value => value.haulIntentOperationId,
                        StringComparer.Ordinal)
                    .ToArray();
            ProductionCapacityRoutingActorQuiesceReceiptSaveData existing =
                drain.actorQuiesceReceipts.FirstOrDefault(value => value != null
                    && string.Equals(
                        value.actorPersistentId,
                        actorId,
                        StringComparison.Ordinal));
            if (existing != null)
            {
                if (!quiescence.TryVerifyDurableReceipt(
                        actor,
                        inventory,
                        drain,
                        existing,
                        out string replayFailure))
                    return Conflict(replayFailure);
                continue;
            }
            if (!haul.TryFreezeForCapacityRoutingQuiescence(
                    actorId,
                    carries,
                    out ProductionCapacityRoutingActorPlanSnapshot plan,
                    out string freezeFailure))
                return Deferred(freezeFailure);
            ProductionCapacityRoutingActorQuiescenceResult quiesced =
                quiescence.TryQuiesceAtCurrentCell(
                    actor,
                    inventory,
                    new ProductionCapacityRoutingActorQuiescenceRequest(
                        stepOperationId,
                        drain.batchCommitId,
                        requestFingerprint,
                        actorId,
                        plan,
                        carries));
            if (!quiesced.IsSuccess)
            {
                return quiesced.Status ==
                    ProductionCapacityRoutingDrainStatus.Conflict
                    ? Conflict(quiesced.FailureReason)
                    : Deferred(quiesced.FailureReason);
            }
            ProductionCapacityRoutingDrainResult confirmed =
                outbox.TryConfirmActorQuiesced(
                    stepOperationId,
                    quiesced.Receipt);
            if (IsFailure(confirmed))
                return confirmed;
            if (!TryCaptureExact(
                    stepOperationId,
                    requestFingerprint,
                    out drain,
                    out ProductionCapacityRoutingDrainResult captureFailure))
                return captureFailure;
        }
        return Current(drain, ProductionCapacityRoutingDrainStatus.Applied);
    }

    private ProductionCapacityRoutingDrainResult TryReleaseEveryActor(
        string stepOperationId,
        string requestFingerprint,
        ProductionCapacityRoutingDrainSaveData drain)
    {
        string[] actorIds = drain.sourceActorCarries
            .Where(value => value != null)
            .Select(value => value.actorPersistentId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        foreach (string actorId in actorIds)
        {
            bool alreadyCommitted = drain.actorAuthorityReleases.Any(value =>
                value != null
                && string.Equals(
                    value.actorPersistentId,
                    actorId,
                    StringComparison.Ordinal)
                && value.effectsCommitted
                && value.actorPlanFinalized);
            if (alreadyCommitted)
                continue;
            ProductionCapacityRoutingDrainResult released = TryReleaseActor(
                stepOperationId,
                requestFingerprint,
                drain,
                actorId);
            if (IsFailure(released))
                return released;
            if (!TryCaptureExact(
                    stepOperationId,
                    requestFingerprint,
                    out drain,
                    out ProductionCapacityRoutingDrainResult captureFailure))
                return captureFailure;
        }
        return Current(drain, ProductionCapacityRoutingDrainStatus.Applied);
    }

    private ProductionCapacityRoutingDrainResult TryReleaseActor(
        string stepOperationId,
        string requestFingerprint,
        ProductionCapacityRoutingDrainSaveData drain,
        string actorId)
    {
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt =
            drain.actorQuiesceReceipts.FirstOrDefault(value => value != null
                && string.Equals(
                    value.actorPersistentId,
                    actorId,
                    StringComparison.Ordinal));
        if (!TryResolveActor(
                actorId,
                out CharacterActor actor,
                out CharacterCarryInventory inventory,
                out AbilityHaul haul))
        {
            return Deferred(
                "production-capacity-routing-actor-runtime-unavailable:"
                + actorId);
        }
        string receiptFailure = string.Empty;
        if (receipt == null
            || !quiescence.TryVerifyDurableReceipt(
                actor,
                inventory,
                drain,
                receipt,
                out receiptFailure))
        {
            return Conflict(receiptFailure.Length > 0
                ? receiptFailure
                : "production-capacity-routing-actor-receipt-missing");
        }

        string[] operationIds = drain.sourceActorCarries
            .Where(value => value != null
                && string.Equals(
                    value.actorPersistentId,
                    actorId,
                    StringComparison.Ordinal))
            .Select(value => value.haulIntentOperationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        ProductionCapacityRoutingActorAuthorityReleaseSaveData release =
            drain.actorAuthorityReleases.FirstOrDefault(value => value != null
                && string.Equals(
                    value.actorPersistentId,
                    actorId,
                    StringComparison.Ordinal));
        if (release == null)
        {
            if (!haul.TryValidateCapacityRoutingQuiescencePlan(
                    operationIds,
                    receipt,
                    out string planFailure))
                return Conflict(planFailure);
            if (!TryCreateReleasePlan(
                    drain,
                    receipt,
                    operationIds,
                    out release,
                    out string createFailure))
                return Conflict(createFailure);
            ProductionCapacityRoutingDrainResult prepared =
                outbox.TryPrepareActorAuthorityRelease(
                    stepOperationId,
                    requestFingerprint,
                    release);
            if (IsFailure(prepared))
                return prepared;
            if (!TryCaptureExact(
                    stepOperationId,
                    requestFingerprint,
                    out drain,
                    out ProductionCapacityRoutingDrainResult captureFailure))
                return captureFailure;
            release = drain.actorAuthorityReleases.FirstOrDefault(value =>
                value != null
                && string.Equals(
                    value.actorPersistentId,
                    actorId,
                    StringComparison.Ordinal));
        }

        string preflightFailure = string.Empty;
        if (release == null
            || !TryPreflightPreparedRelease(
                release,
                receipt,
                haul,
                out bool actorPlanAlreadyFinalized,
                out preflightFailure))
        {
            return Conflict(preflightFailure.Length > 0
                ? preflightFailure
                : "production-capacity-routing-authority-plan-missing");
        }

        for (int rowIndex = 0; rowIndex < release.operations.Count; rowIndex++)
        {
            ProductionCapacityRoutingOperationAuthorityRowSaveData row =
                release.operations[rowIndex];
#if UNITY_EDITOR
            if (DebugFailBeforeAuthorityRowMutation?.Invoke(
                    row.operationId,
                    rowIndex) == true)
            {
                return Deferred(
                    "injected-capacity-routing-authority-row-failure:"
                    + row.operationId);
            }
#endif
            if (reservations.TryReleaseExactOwnedSet(
                    row.operationId,
                    row.quantityLeaseIds,
                    ItemReservationReleaseReason.Cancelled,
                    release.planFingerprint,
                    out string leaseFailure) ==
                ExactAuthorityReleaseStatus.Conflict)
                return Conflict(leaseFailure);
            if (admissions.TryReleaseExactOwnedSet(
                    row.operationId,
                    row.warehouseAdmissionTokenIds,
                    WarehouseMassAdmissionReleaseReason.DestinationInvalidated,
                    release.planFingerprint,
                    out string admissionFailure) ==
                ExactAuthorityReleaseStatus.Conflict)
                return Conflict(admissionFailure);
            if (repository.HaulDeliveryIntents.TryRemoveExact(
                    row.operationId,
                    row.haulIntentFingerprint,
                    release.planFingerprint,
                    out string intentFailure) ==
                ExactAuthorityReleaseStatus.Conflict)
                return Conflict(intentFailure);
        }

        if (!actorPlanAlreadyFinalized
            && !haul.TryFinalizeCapacityRoutingQuiescence(
                release.operationIds,
                receipt,
                out string finalizeFailure))
            return Conflict(finalizeFailure);
        if (!quiescence.TryVerifyDurableReceipt(
                actor,
                inventory,
                drain,
                receipt,
                out string finalReceiptFailure))
            return Conflict(finalReceiptFailure);

        string effectFingerprint = ProductionCapacityRoutingDrainFingerprint
            .CreateActorAuthorityReleaseEffectFingerprint(
                release.planFingerprint,
                actorPlanFinalized: true);
        return outbox.TryCommitActorAuthorityRelease(
            stepOperationId,
            release.planFingerprint,
            effectFingerprint,
            actorPlanFinalized: true);
    }

    private bool TryCreateReleasePlan(
        ProductionCapacityRoutingDrainSaveData drain,
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt,
        IReadOnlyList<string> operationIds,
        out ProductionCapacityRoutingActorAuthorityReleaseSaveData release,
        out string failureReason)
    {
        release = null;
        failureReason = string.Empty;
        List<ProductionCapacityRoutingOperationAuthorityRowSaveData> rows = new();
        foreach (string operationId in operationIds)
        {
            if (!reservations.TryGetLeasesByOwner(
                    operationId,
                    out IReadOnlyList<ItemQuantityLease> leases)
                || !repository.HaulDeliveryIntents.TryCapture(
                    operationId,
                    out HaulDeliveryIntentSaveData intent)
                || intent == null
                || !string.Equals(
                    intent.ownerCharacterId,
                    receipt.actorPersistentId,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "production-capacity-routing-live-authority-missing:"
                    + operationId;
                return false;
            }
            rows.Add(new ProductionCapacityRoutingOperationAuthorityRowSaveData
            {
                operationId = operationId,
                quantityLeaseIds = leases.Select(value => value.leaseId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList(),
                warehouseAdmissionTokenIds = (intent.warehouseAdmissions
                        ?? new List<WarehouseHaulAdmissionSaveData>())
                    .Where(value => value != null)
                    .Select(value => value.tokenId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList(),
                haulIntentFingerprint = HaulDeliveryIntentRuntime
                    .CreateCapacityRoutingAuthorityFingerprint(intent)
            });
        }
        release = new ProductionCapacityRoutingActorAuthorityReleaseSaveData
        {
            actorPersistentId = receipt.actorPersistentId,
            actorQuiesceReceiptFingerprint = receipt.receiptFingerprint,
            operationIds = operationIds.ToList(),
            operations = rows,
            activePlanFingerprint = receipt.activePlanFingerprint
        };
        release.planFingerprint = ProductionCapacityRoutingDrainFingerprint
            .CreateActorAuthorityReleasePlanFingerprint(
                drain.stepOperationId,
                drain.requestFingerprint,
                release);
        return true;
    }

    private bool TryPreflightPreparedRelease(
        ProductionCapacityRoutingActorAuthorityReleaseSaveData release,
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt,
        AbilityHaul haul,
        out bool actorPlanAlreadyFinalized,
        out string failureReason)
    {
        actorPlanAlreadyFinalized = false;
        failureReason = string.Empty;
        foreach (ProductionCapacityRoutingOperationAuthorityRowSaveData row in
                 release.operations)
        {
            string[] liveLeases = reservations.TryGetLeasesByOwner(
                    row.operationId,
                    out IReadOnlyList<ItemQuantityLease> leases)
                ? leases.Select(value => value.leaseId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();
            if (liveLeases.Length != 0
                && !liveLeases.SequenceEqual(
                    row.quantityLeaseIds,
                    StringComparer.Ordinal))
            {
                failureReason =
                    "production-capacity-routing-live-lease-set-conflict:"
                    + row.operationId;
                return false;
            }
            foreach (string tokenId in row.warehouseAdmissionTokenIds)
            {
                if (!admissions.TryGetStatus(
                        tokenId,
                        out WarehouseMassAdmissionStatusSnapshot status)
                    || status.Status is not (
                        WarehouseMassAdmissionTokenStatus.Reserved
                        or WarehouseMassAdmissionTokenStatus.Released)
                    || status.Status == WarehouseMassAdmissionTokenStatus.Released
                        && status.ReleaseReason !=
                            WarehouseMassAdmissionReleaseReason
                                .DestinationInvalidated)
                {
                    failureReason =
                        "production-capacity-routing-live-admission-conflict:"
                        + tokenId;
                    return false;
                }
            }
            if (repository.HaulDeliveryIntents.TryCapture(
                    row.operationId,
                    out HaulDeliveryIntentSaveData intent)
                && !string.Equals(
                    HaulDeliveryIntentRuntime
                        .CreateCapacityRoutingAuthorityFingerprint(intent),
                    row.haulIntentFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "production-capacity-routing-live-intent-conflict:"
                    + row.operationId;
                return false;
            }
        }

        if (haul.IsCapacityRoutingQuiescenceFrozen)
        {
            return haul.TryValidateCapacityRoutingQuiescencePlan(
                release.operationIds,
                receipt,
                out failureReason);
        }
        actorPlanAlreadyFinalized = haul.CaptureActiveHaulOperationIds().Count == 0
            && release.operations.All(row =>
                !reservations.TryGetLeasesByOwner(
                    row.operationId,
                    out IReadOnlyList<ItemQuantityLease> leases)
                || leases.Count == 0)
            && release.operations.All(row =>
                !repository.HaulDeliveryIntents.TryCapture(
                    row.operationId,
                    out _));
        if (!actorPlanAlreadyFinalized)
        {
            failureReason =
                "production-capacity-routing-frozen-plan-state-conflict";
        }
        return actorPlanAlreadyFinalized;
    }

    private bool TryResolveActor(
        string actorId,
        out CharacterActor actor,
        out CharacterCarryInventory inventory,
        out AbilityHaul haul)
    {
        inventory = inventories.Find((CharacterId)actorId);
        actor = inventory != null
            ? inventory.GetComponent<CharacterActor>()
            : null;
        haul = actor != null ? actor.GetComponent<AbilityHaul>() : null;
        return inventory != null && actor != null && haul != null;
    }

    private bool TryCaptureExact(
        string stepOperationId,
        string requestFingerprint,
        out ProductionCapacityRoutingDrainSaveData drain,
        out ProductionCapacityRoutingDrainResult failure)
    {
        if (!outbox.TryCapture(stepOperationId, out drain))
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

    private static bool IsFailure(
        ProductionCapacityRoutingDrainResult value) =>
        value.Status is ProductionCapacityRoutingDrainStatus.Conflict
            or ProductionCapacityRoutingDrainStatus.Deferred;

    private static ProductionCapacityRoutingDrainResult Current(
        ProductionCapacityRoutingDrainSaveData drain,
        ProductionCapacityRoutingDrainStatus status) => new(
            status,
            drain.commitId,
            drain.receiptFingerprint,
            string.Empty);

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
