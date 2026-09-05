using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

public interface IProductionInputDestinationCustodyDrainService
{
    /// <summary>
    /// The owning executor must repeatedly call TryCommit in the same simulation
    /// command until it reaches a terminal/deferred/conflict result. On restore,
    /// this recovery must run before AI, movement, lease expiry, or hauling ticks.
    /// </summary>
    bool RequiresImmediateRecoveryBeforeGameplayTick { get; }

    bool TryCaptureSource(
        string sourceDestinationId,
        out ProductionInputDestinationCustodySourceSnapshot snapshot,
        out string failureReason);

    bool TryBuildRequest(
        string parentOperationId,
        string stepOperationId,
        string ownerStableId,
        string billId,
        string facilityId,
        Vector2Int ownerPosition,
        string sourceClaimFingerprint,
        ProductionInputDestinationCustodySourceSnapshot snapshot,
        out ProductionInputDestinationCustodyDrainRequest request,
        out string failureReason);

    bool TryCaptureRequest(
        string parentOperationId,
        string stepOperationId,
        string ownerStableId,
        string billId,
        string facilityId,
        string sourceDestinationId,
        Vector2Int ownerPosition,
        string sourceClaimFingerprint,
        out ProductionInputDestinationCustodyDrainRequest request,
        out string failureReason);

    ProductionInputDestinationCustodyDrainResult TryPrepare(
        ProductionInputDestinationCustodyDrainRequest request);

    ProductionInputDestinationCustodyDrainResult TryCommit(
        string stepOperationId,
        string requestFingerprint);

    ProductionInputDestinationCustodyDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint);

    ProductionInputDestinationCustodyDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint);

    bool TryCapture(
        string stepOperationId,
        out ProductionInputDestinationCustodyDrainSaveData record);
}

/// <summary>
/// Executes the physical side of a generic-production input destination drain.
/// The Items outbox is the durable progress authority. Each commit call performs
/// at most one actor stop/drop, one operation authority release, or the terminal
/// destination release and records that effect while the item capture barrier is
/// held.
/// </summary>
public sealed class ProductionInputDestinationCustodyDrainService :
    IProductionInputDestinationCustodyDrainService,
    IProductionInputDestinationCustodyDrainCheckpointGcPort,
    IProductionInputDestinationCustodyDrainLiveQuery
{
    private const string InterruptionReason =
        "production-input-destination-destructive-drain";

    private readonly IProductionInputDestinationCustodyDrainOutbox outbox;
    private readonly IWorldItemStackRuntime worldItems;
    private readonly IItemQuantityReservationService reservations;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IItemTransferService transfers;
    private readonly IItemReservationMutationGate mutationGate;

    public bool RequiresImmediateRecoveryBeforeGameplayTick => true;

    public IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData>
        CaptureAll()
    {
        if (outbox is not IProductionInputDestinationCustodyDrainLiveQuery query)
        {
            throw new InvalidOperationException(
                "production-input-destination-live-query-missing");
        }
        return query.CaptureAll();
    }

    public ProductionInputDestinationCustodyDrainService(
        IProductionInputDestinationCustodyDrainOutbox outbox,
        IWorldItemStackRuntime worldItems,
        IItemQuantityReservationService reservations,
        ICharacterWorldQuery characterWorld,
        IPhysicalItemMassQuery massQuery,
        IItemTransferService transfers,
        IItemReservationMutationGate mutationGate)
    {
        this.outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        this.worldItems = worldItems
            ?? throw new ArgumentNullException(nameof(worldItems));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.transfers = transfers
            ?? throw new ArgumentNullException(nameof(transfers));
        this.mutationGate = mutationGate
            ?? throw new ArgumentNullException(nameof(mutationGate));
        if (!ReferenceEquals(worldItems.MassQuery, massQuery))
        {
            throw new ArgumentException(
                "The input drain must use the world item's mass authority.",
                nameof(massQuery));
        }
    }

    public bool TryCaptureRequest(
        string parentOperationId,
        string stepOperationId,
        string ownerStableId,
        string billId,
        string facilityId,
        string sourceDestinationId,
        Vector2Int ownerPosition,
        string sourceClaimFingerprint,
        out ProductionInputDestinationCustodyDrainRequest request,
        out string failureReason)
    {
        if (!TryCaptureSource(
                sourceDestinationId,
                out ProductionInputDestinationCustodySourceSnapshot snapshot,
                out failureReason))
        {
            request = null;
            return false;
        }

        return TryBuildRequest(
            parentOperationId,
            stepOperationId,
            ownerStableId,
            billId,
            facilityId,
            ownerPosition,
            sourceClaimFingerprint,
            snapshot,
            out request,
            out failureReason);
    }

    public bool TryCaptureSource(
        string sourceDestinationId,
        out ProductionInputDestinationCustodySourceSnapshot snapshot,
        out string failureReason)
    {
        snapshot = null;
        failureReason = string.Empty;
        if (!IsToken(sourceDestinationId))
        {
            failureReason =
                "production-input-destination-drain-capture-identity-invalid";
            return false;
        }

        IDisposable captureBarrier;
        try
        {
            captureBarrier = mutationGate.EnterCaptureBarrier();
        }
        catch (Exception exception)
        {
            failureReason =
                "production-input-destination-drain-capture-barrier-failed:"
                + exception.GetType().Name;
            return false;
        }
        using IDisposable barrier = captureBarrier;

        WorldItemStackSnapshot[] allStacks;
        HaulDeliveryIntentSaveData[] intents;
        CharacterActor[] actors;
        try
        {
            allStacks = (worldItems.GetAllStacks()
                    ?? Array.Empty<WorldItemStackSnapshot>())
                .Where(value => value != null && value.Quantity > 0)
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
            intents = (worldItems.CaptureHaulDeliveryIntentsByDestination(
                        sourceDestinationId)
                    ?? Array.Empty<HaulDeliveryIntentSaveData>())
                .Where(value => value != null)
                .OrderBy(value => value.operationId, StringComparer.Ordinal)
                .ToArray();
            actors = (characterWorld.Characters ?? Array.Empty<CharacterActor>())
                .Where(value => value != null)
                .OrderBy(ActorId, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception)
        {
            failureReason =
                "production-input-destination-drain-capture-read-failed:"
                + exception.GetType().Name;
            return false;
        }

        if (intents.Select(value => value.operationId)
                .Distinct(StringComparer.Ordinal).Count() != intents.Length
            || actors.Select(ActorId).Any(value => !IsToken(value))
            || actors.Select(ActorId).Distinct(StringComparer.Ordinal).Count()
                != actors.Length
            || allStacks.Select(value => value.StackId)
                .Distinct(StringComparer.Ordinal).Count() != allStacks.Length)
        {
            failureReason =
                "production-input-destination-drain-capture-authority-duplicate";
            return false;
        }

        Dictionary<string, WorldItemStackSnapshot> stacksById = allStacks
            .ToDictionary(value => value.StackId, StringComparer.Ordinal);
        HashSet<string> sourceStackIds = allStacks
            .Where(value => string.Equals(
                value.DestinationId,
                sourceDestinationId,
                StringComparison.Ordinal))
            .Select(value => value.StackId)
            .ToHashSet(StringComparer.Ordinal);

        List<ProductionInputDestinationDrainOperationSaveData> operationRows =
            new();
        Dictionary<string, List<string>> operationsByActor =
            new(StringComparer.Ordinal);
        foreach (HaulDeliveryIntentSaveData intent in intents)
        {
            if (!TryCaptureOperation(
                    intent,
                    sourceDestinationId,
                    stacksById,
                    actors,
                    sourceStackIds,
                    out ProductionInputDestinationDrainOperationSaveData row,
                    out bool requiresActor,
                    out failureReason))
            {
                return false;
            }
            operationRows.Add(row);
            if (requiresActor)
            {
                if (!operationsByActor.TryGetValue(
                        row.actorId,
                        out List<string> actorOperations))
                {
                    actorOperations = new List<string>();
                    operationsByActor.Add(row.actorId, actorOperations);
                }
                actorOperations.Add(row.operationId);
            }
        }

        List<ProductionInputDestinationDrainActorSaveData> actorRows = new();
        foreach (KeyValuePair<string, List<string>> pair in operationsByActor
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            CharacterActor actor = actors.Single(value => string.Equals(
                ActorId(value), pair.Key, StringComparison.Ordinal));
            string[] allowed = pair.Value
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!TryValidateActorClosure(actor, allowed, out failureReason))
                return false;
            actorRows.Add(new ProductionInputDestinationDrainActorSaveData
            {
                actorId = pair.Key,
                allowedOperationIds = allowed.ToList(),
                sourcePhysicalFingerprint = CreateActorFingerprint(actor, allowed)
            });
        }

        if (!TryValidateReservationClosure(
                sourceStackIds,
                operationRows.Select(value => value.operationId),
                stacksById,
                out failureReason))
        {
            return false;
        }

        List<ProductionInputDestinationDrainStackSaveData> stackRows = new();
        int inputQuantity = 0;
        long inputMassGrams = 0L;
        try
        {
            foreach (string stackId in sourceStackIds.OrderBy(
                         value => value,
                         StringComparer.Ordinal))
            {
                if (!stacksById.TryGetValue(
                        stackId,
                        out WorldItemStackSnapshot stack)
                    || !TryCaptureStack(stack, out var stackRow,
                        out failureReason))
                {
                    failureReason = string.IsNullOrEmpty(failureReason)
                        ? "production-input-destination-drain-source-stack-missing:"
                            + stackId
                        : failureReason;
                    return false;
                }
                stackRows.Add(stackRow);
                inputQuantity = checked(inputQuantity + stackRow.quantity);
                inputMassGrams = checked(
                    inputMassGrams + stackRow.massGrams);
            }
        }
        catch (Exception exception) when (exception is OverflowException
            or ArgumentException or InvalidOperationException)
        {
            failureReason =
                "production-input-destination-drain-source-stack-invalid:"
                + exception.GetType().Name;
            return false;
        }

        long authorityRevision = massQuery.AuthorityRevision;
        string ownershipFingerprint = CreateOwnershipFingerprint(
            sourceDestinationId,
            authorityRevision,
            stackRows,
            operationRows,
            actorRows,
            inputQuantity,
            inputMassGrams);
        snapshot = new ProductionInputDestinationCustodySourceSnapshot(
            sourceDestinationId,
            authorityRevision,
            ownershipFingerprint,
            stackRows,
            operationRows,
            actorRows,
            inputQuantity,
            inputMassGrams);
        return true;
    }

    public bool TryBuildRequest(
        string parentOperationId,
        string stepOperationId,
        string ownerStableId,
        string billId,
        string facilityId,
        Vector2Int ownerPosition,
        string sourceClaimFingerprint,
        ProductionInputDestinationCustodySourceSnapshot snapshot,
        out ProductionInputDestinationCustodyDrainRequest request,
        out string failureReason)
    {
        request = null;
        failureReason = string.Empty;
        if (!IsToken(parentOperationId)
            || !IsToken(stepOperationId)
            || !IsToken(ownerStableId)
            || !IsToken(billId)
            || !IsToken(facilityId)
            || snapshot == null
            || !ProductionInputDestinationCustodyDrainContract
                .IsValidSourceSnapshot(snapshot)
            || !IsToken(snapshot.SourceDestinationId)
            || snapshot.MassAuthorityRevision < 0L
            || snapshot.MassAuthorityRevision != massQuery.AuthorityRevision
            || !IsDigest(snapshot.SourceOwnershipFingerprint)
            || !IsDigest(sourceClaimFingerprint))
        {
            failureReason =
                "production-input-destination-drain-build-identity-invalid";
            return false;
        }
        string expectedOwnership = CreateOwnershipFingerprint(
            snapshot.SourceDestinationId,
            snapshot.MassAuthorityRevision,
            snapshot.SourceStacks,
            snapshot.SourceOperations,
            snapshot.SourceActors,
            snapshot.InputQuantity,
            snapshot.InputMassGrams);
        if (!string.Equals(
                expectedOwnership,
                snapshot.SourceOwnershipFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "production-input-destination-drain-build-source-drift";
            return false;
        }
        string requestFingerprint =
            ProductionInputDestinationCustodyDrainFingerprint.CreateRequest(
                parentOperationId,
                stepOperationId,
                ownerStableId,
                billId,
                facilityId,
                snapshot.SourceDestinationId,
                ownerPosition.x,
                ownerPosition.y,
                sourceClaimFingerprint,
                snapshot.SourceOwnershipFingerprint,
                snapshot.SourceStacks,
                snapshot.SourceOperations,
                snapshot.SourceActors,
                snapshot.InputQuantity,
                snapshot.InputMassGrams);
        request = new ProductionInputDestinationCustodyDrainRequest(
            parentOperationId,
            stepOperationId,
            ownerStableId,
            billId,
            facilityId,
            snapshot.SourceDestinationId,
            ownerPosition.x,
            ownerPosition.y,
            sourceClaimFingerprint,
            snapshot.SourceOwnershipFingerprint,
            snapshot.SourceStacks,
            snapshot.SourceOperations,
            snapshot.SourceActors,
            snapshot.InputQuantity,
            snapshot.InputMassGrams,
            requestFingerprint);
        if (!ProductionInputDestinationCustodyDrainContract.IsValidRequest(
                request))
        {
            request = null;
            failureReason =
                "production-input-destination-drain-build-request-invalid";
            return false;
        }
        return true;
    }

    [GameplayInternalOnly(
        "Persists an immutable generic-production input destination custody drain before any physical release.",
        "Generic production destructive-drain participant only")]
    public ProductionInputDestinationCustodyDrainResult TryPrepare(
        ProductionInputDestinationCustodyDrainRequest request) =>
        outbox.TryPrepare(request);

    [GameplayInternalOnly(
        "Advances one durable generic-production input destination custody effect while physical capture is fenced.",
        "Generic production destructive-drain participant only")]
    public ProductionInputDestinationCustodyDrainResult TryCommit(
        string stepOperationId,
        string requestFingerprint)
    {
        if (!outbox.TryCapture(
                stepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData state))
        {
            return Conflict("production-input-destination-drain-missing");
        }
        if (!string.Equals(
                state.requestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict(
                "production-input-destination-drain-request-conflict");
        }
        if (state.phase is ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck
            or ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc)
        {
            return Current(state,
                ProductionInputDestinationCustodyDrainStatus.Replay);
        }

        try
        {
            return state.phase switch
            {
                ProductionInputDestinationCustodyDrainPhase.Prepared =>
                    BeginDrain(state),
                ProductionInputDestinationCustodyDrainPhase.ReleasingActors =>
                    ReleaseNextActor(state),
                ProductionInputDestinationCustodyDrainPhase
                    .ReleasingOperationAuthority => ReleaseNextOperation(state),
                ProductionInputDestinationCustodyDrainPhase
                    .ReleasingDestination => ReleaseDestination(state),
                _ => Conflict(
                    "production-input-destination-drain-phase-invalid")
            };
        }
        catch (Exception exception) when (exception is OverflowException
            or ArgumentException or InvalidOperationException)
        {
            return Conflict(
                "production-input-destination-drain-execution-failed:"
                + exception.GetType().Name);
        }
    }

    [GameplayInternalOnly(
        "Records the generic production bill's durable acknowledgement of an input destination drain receipt.",
        "Generic production destructive-drain participant only")]
    public ProductionInputDestinationCustodyDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint) => outbox.TryAcknowledge(
        stepOperationId,
        receiptFingerprint);

    [GameplayInternalOnly(
        "Removes an acknowledged input destination custody receipt after the owning bill checkpoint has committed.",
        "Generic production destructive-drain checkpoint collector only")]
    public ProductionInputDestinationCustodyDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint) => outbox.TryGarbageCollect(
        stepOperationId,
        receiptFingerprint);

    public bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData> records,
        out IProductionInputDestinationCustodyDrainCheckpointGcCandidate candidate,
        out string failureReason)
    {
        if (outbox is not IProductionInputDestinationCustodyDrainCheckpointGcPort gc)
        {
            candidate = null;
            failureReason =
                "production-input-destination-checkpoint-gc-port-missing";
            return false;
        }
        return gc.TryPrepareCheckpointGarbageCollection(
            records,
            out candidate,
            out failureReason);
    }

    public bool TryPublishCheckpointGarbageCollection(
        IProductionInputDestinationCustodyDrainCheckpointGcCandidate candidate,
        out string failureReason)
    {
        if (outbox is not IProductionInputDestinationCustodyDrainCheckpointGcPort gc)
        {
            failureReason =
                "production-input-destination-checkpoint-gc-port-missing";
            return false;
        }
        return gc.TryPublishCheckpointGarbageCollection(
            candidate,
            out failureReason);
    }

    public void RollbackCheckpointGarbageCollection(
        IProductionInputDestinationCustodyDrainCheckpointGcCandidate candidate)
    {
        if (outbox is not IProductionInputDestinationCustodyDrainCheckpointGcPort gc)
        {
            throw new InvalidOperationException(
                "production-input-destination-checkpoint-gc-port-missing");
        }
        gc.RollbackCheckpointGarbageCollection(candidate);
    }

    public void CompleteCheckpointGarbageCollection(
        IProductionInputDestinationCustodyDrainCheckpointGcCandidate candidate)
    {
        if (outbox is not IProductionInputDestinationCustodyDrainCheckpointGcPort gc)
        {
            throw new InvalidOperationException(
                "production-input-destination-checkpoint-gc-port-missing");
        }
        gc.CompleteCheckpointGarbageCollection(candidate);
    }

    public bool TryCapture(
        string stepOperationId,
        out ProductionInputDestinationCustodyDrainSaveData record) =>
        outbox.TryCapture(stepOperationId, out record);

    private ProductionInputDestinationCustodyDrainResult BeginDrain(
        ProductionInputDestinationCustodyDrainSaveData state)
    {
        if (!TryVerifyInitialSource(state, out string failureReason))
            return Conflict(failureReason);
        using IDisposable barrier = mutationGate.EnterCaptureBarrier();
        return outbox.TryBeginDraining(
            state.stepOperationId,
            state.requestFingerprint);
    }

    private ProductionInputDestinationCustodyDrainResult ReleaseNextActor(
        ProductionInputDestinationCustodyDrainSaveData state)
    {
        ProductionInputDestinationDrainActorSaveData actorRow = state.sourceActors
            .Skip(state.completedActorIds.Count)
            .FirstOrDefault();
        if (actorRow == null)
            return outbox.TryBeginReleasingOperationAuthority(
                state.stepOperationId);

        ProductionInputDestinationDrainOperationSaveData[] actorOperations =
            state.sourceOperations.Where(value => actorRow.allowedOperationIds
                    .Contains(value.operationId))
                .ToArray();
        if (actorOperations.Length == actorRow.allowedOperationIds.Count
            && actorOperations.All(value => !value.hadCommittedPickup
                && !HasAnyOperationAuthority(value.operationId)))
        {
            using IDisposable absentBarrier = mutationGate.EnterCaptureBarrier();
            return outbox.TryRecordActorCompleted(
                state.stepOperationId,
                actorRow.actorId);
        }

        if (!TryFindActor(actorRow.actorId, out CharacterActor actor,
                out string failureReason))
        {
            return Conflict(failureReason);
        }
        string[] allowed = actorRow.allowedOperationIds.ToArray();
        if (!TryValidateActorClosure(actor, allowed, out failureReason)
            || !string.Equals(
                CreateActorFingerprint(actor, allowed),
                actorRow.sourcePhysicalFingerprint,
                StringComparison.Ordinal)
            || !TryVerifyActorSourceRows(
                state,
                actor,
                allowed,
                out failureReason))
        {
            return Conflict(string.IsNullOrEmpty(failureReason)
                ? "production-input-destination-drain-actor-source-drift:"
                    + actorRow.actorId
                : failureReason);
        }

        AbilityHaul haul = actor.GetComponent<AbilityHaul>();
        if (haul == null)
        {
            return Conflict(
                "production-input-destination-drain-actor-haul-missing:"
                + actorRow.actorId);
        }
        using (mutationGate.EnterCaptureBarrier())
        {
            if (!haul.TryStopHaulingOrReleaseRestoredCarryIfOperationsSubsetOf(
                    allowed,
                    InterruptionReason,
                    HaulInterruptionDisposition
                        .ReleaseUnpickedAndDropCarriedAtActor,
                    out string stopFailure))
            {
                return Deferred(
                    "production-input-destination-drain-actor-release-deferred:"
                    + actorRow.actorId + ":" + stopFailure);
            }
            if (!TryVerifyActorReleased(
                    actor,
                    allowed,
                    state,
                    out failureReason))
            {
                return Conflict(failureReason);
            }
            return outbox.TryRecordActorCompleted(
                state.stepOperationId,
                actorRow.actorId);
        }
    }

    private ProductionInputDestinationCustodyDrainResult ReleaseNextOperation(
        ProductionInputDestinationCustodyDrainSaveData state)
    {
        ProductionInputDestinationDrainOperationSaveData operation =
            state.sourceOperations.Skip(state.releasedOperationIds.Count)
                .FirstOrDefault();
        if (operation == null)
            return outbox.TryBeginReleasingDestination(state.stepOperationId);

        bool hasIntent = worldItems.TryCaptureHaulDeliveryIntent(
            operation.operationId,
            out HaulDeliveryIntentSaveData intent);
        bool hasLeases = reservations.TryGetLeasesByOwner(
                operation.operationId,
                out IReadOnlyList<ItemQuantityLease> leaseValues)
            && leaseValues != null
            && leaseValues.Count > 0;
        bool actorAuthority = AnyActorOwnsOrCarries(operation.operationId);
        if (!hasIntent && !hasLeases && !actorAuthority)
        {
            // A pre-pickup plan may expire, and an actor stop/drop releases a
            // committed plan as part of that same fenced effect. Both absences
            // are monotonic replay states.
            return outbox.TryRecordOperationReleased(
                state.stepOperationId,
                operation.operationId);
        }
        if (actorAuthority)
        {
            return Conflict(
                "production-input-destination-drain-operation-actor-active:"
                + operation.operationId);
        }
        if (!hasIntent)
        {
            return Conflict(
                "production-input-destination-drain-operation-intent-missing-with-lease:"
                + operation.operationId);
        }
        if (!TryCaptureOperationAuthority(
                intent,
                leaseValues ?? Array.Empty<ItemQuantityLease>(),
                out string currentFingerprint,
                out _,
                out _,
                out string failureReason)
            || !string.Equals(
                currentFingerprint,
                operation.operationFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict(string.IsNullOrEmpty(failureReason)
                ? "production-input-destination-drain-operation-source-drift:"
                    + operation.operationId
                : failureReason);
        }

        using (mutationGate.EnterCaptureBarrier())
        {
            reservations.ReleaseByOwner(
                operation.operationId,
                ItemReservationReleaseReason.Cancelled);
            if (worldItems.TryCaptureHaulDeliveryIntent(
                    operation.operationId,
                    out _)
                && !worldItems.ReleaseHaulDeliveryIntent(
                    operation.operationId))
            {
                return Conflict(
                    "production-input-destination-drain-intent-release-failed:"
                    + operation.operationId);
            }
            if (reservations.TryGetLeasesByOwner(
                    operation.operationId,
                    out IReadOnlyList<ItemQuantityLease> remaining)
                && remaining != null && remaining.Count > 0
                || worldItems.TryCaptureHaulDeliveryIntent(
                    operation.operationId,
                    out _)
                || AnyActorOwnsOrCarries(operation.operationId))
            {
                return Conflict(
                    "production-input-destination-drain-operation-release-incomplete:"
                    + operation.operationId);
            }
            return outbox.TryRecordOperationReleased(
                state.stepOperationId,
                operation.operationId);
        }
    }

    private ProductionInputDestinationCustodyDrainResult ReleaseDestination(
        ProductionInputDestinationCustodyDrainSaveData state)
    {
        foreach (ProductionInputDestinationDrainOperationSaveData operation in
                 state.sourceOperations)
        {
            if (worldItems.TryCaptureHaulDeliveryIntent(
                    operation.operationId,
                    out _)
                || reservations.TryGetLeasesByOwner(
                    operation.operationId,
                    out IReadOnlyList<ItemQuantityLease> leases)
                    && leases != null && leases.Count > 0
                || AnyActorOwnsOrCarries(operation.operationId))
            {
                return Deferred(
                    "production-input-destination-drain-operation-still-active:"
                    + operation.operationId);
            }
        }

        if (!TryCaptureFrozenStacks(
                state,
                requireReleased: false,
                out Dictionary<string, WorldItemStackSnapshot> before,
                out string failureReason))
        {
            return Conflict(failureReason);
        }
        HashSet<string> frozenIds = state.sourceStacks
            .Select(value => value.stackId)
            .ToHashSet(StringComparer.Ordinal);
        string foreign = (worldItems.GetAllStacks()
                ?? Array.Empty<WorldItemStackSnapshot>())
            .Where(value => value != null && value.Quantity > 0
                && string.Equals(
                    value.DestinationId,
                    state.sourceDestinationId,
                    StringComparison.Ordinal))
            .Select(value => value.StackId)
            .FirstOrDefault(value => !frozenIds.Contains(value));
        if (!string.IsNullOrEmpty(foreign))
        {
            return Conflict(
                "production-input-destination-drain-late-destination-stack:"
                + foreign);
        }

        using (mutationGate.EnterCaptureBarrier())
        {
            transfers.ReleaseDestination(
                state.sourceDestinationId,
                new Vector2Int(state.ownerGridX, state.ownerGridY));
            if (!TryCaptureFrozenStacks(
                    state,
                    requireReleased: true,
                    out Dictionary<string, WorldItemStackSnapshot> after,
                    out failureReason))
            {
                return Conflict(failureReason);
            }
            string resultFingerprint = CreateResultFingerprint(state, after);
            return outbox.TryCommitEffect(
                state.stepOperationId,
                after.Keys.OrderBy(value => value, StringComparer.Ordinal),
                state.inputQuantity,
                state.inputMassGrams,
                resultFingerprint);
        }
    }

    private bool TryVerifyInitialSource(
        ProductionInputDestinationCustodyDrainSaveData state,
        out string failureReason)
    {
        failureReason = string.Empty;
        Dictionary<string, WorldItemStackSnapshot> live = (worldItems.GetAllStacks()
                ?? Array.Empty<WorldItemStackSnapshot>())
            .Where(value => value != null && value.Quantity > 0)
            .ToDictionary(value => value.StackId, StringComparer.Ordinal);
        Dictionary<string, CharacterActor> actors = CurrentActorsById(
            out failureReason);
        if (actors == null)
            return false;

        HashSet<string> vanishedUnpickedOperations = state.sourceOperations
            .Where(value => !value.hadCommittedPickup
                && !HasAnyOperationAuthority(value.operationId))
            .Select(value => value.operationId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (ProductionInputDestinationDrainStackSaveData stack in
                 state.sourceStacks)
        {
            if (!live.TryGetValue(stack.stackId, out WorldItemStackSnapshot value)
                || !StackInvariantMatches(stack, value)
                || !StrictStackLocationMatches(
                    state,
                    stack,
                    value,
                    allowReservationRevisionAdvance:
                        vanishedUnpickedOperations.Count > 0)
                || HasForeignLease(
                    stack.stackId,
                    state.sourceOperations.Select(operation =>
                        operation.operationId))
                || !ReservationLedgerMatches(value))
            {
                failureReason =
                    "production-input-destination-drain-source-stack-drift:"
                    + stack.stackId;
                return false;
            }
        }
        foreach (ProductionInputDestinationDrainOperationSaveData operation in
                 state.sourceOperations)
        {
            if (vanishedUnpickedOperations.Contains(operation.operationId))
                continue;
            if (!worldItems.TryCaptureHaulDeliveryIntent(
                    operation.operationId,
                    out HaulDeliveryIntentSaveData intent)
                || !reservations.TryGetLeasesByOwner(
                    operation.operationId,
                    out IReadOnlyList<ItemQuantityLease> leases)
                || !TryCaptureOperationAuthority(
                    intent,
                    leases,
                    out string fingerprint,
                    out _,
                    out _,
                    out failureReason)
                || !string.Equals(
                    fingerprint,
                    operation.operationFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason = string.IsNullOrEmpty(failureReason)
                    ? "production-input-destination-drain-source-operation-drift:"
                        + operation.operationId
                    : failureReason;
                return false;
            }
        }
        foreach (ProductionInputDestinationDrainActorSaveData actorRow in
                 state.sourceActors)
        {
            if (actorRow.allowedOperationIds.All(
                    vanishedUnpickedOperations.Contains))
            {
                continue;
            }
            if (!actors.TryGetValue(actorRow.actorId, out CharacterActor actor)
                || !TryValidateActorClosure(
                    actor,
                    actorRow.allowedOperationIds,
                    out failureReason)
                || !string.Equals(
                    CreateActorFingerprint(actor, actorRow.allowedOperationIds),
                    actorRow.sourcePhysicalFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason = string.IsNullOrEmpty(failureReason)
                    ? "production-input-destination-drain-source-actor-drift:"
                        + actorRow.actorId
                    : failureReason;
                return false;
            }
        }
        string ownership = CreateOwnershipFingerprint(
            state.sourceDestinationId,
            massQuery.AuthorityRevision,
            state.sourceStacks,
            state.sourceOperations,
            state.sourceActors,
            state.inputQuantity,
            state.inputMassGrams);
        if (!string.Equals(
                ownership,
                state.sourceOwnershipFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "production-input-destination-drain-source-ownership-drift";
            return false;
        }
        return true;
    }

    private bool TryVerifyActorSourceRows(
        ProductionInputDestinationCustodyDrainSaveData state,
        CharacterActor actor,
        IReadOnlyCollection<string> allowedOperations,
        out string failureReason)
    {
        failureReason = string.Empty;
        Dictionary<string, WorldItemStackSnapshot> live = (worldItems.GetAllStacks()
                ?? Array.Empty<WorldItemStackSnapshot>())
            .Where(value => value != null && value.Quantity > 0)
            .ToDictionary(value => value.StackId, StringComparer.Ordinal);
        foreach (ProductionInputDestinationDrainOperationSaveData operation in
                 state.sourceOperations.Where(value => allowedOperations.Contains(
                     value.operationId)))
        {
            if (!worldItems.TryCaptureHaulDeliveryIntent(
                    operation.operationId,
                    out HaulDeliveryIntentSaveData intent)
                || !reservations.TryGetLeasesByOwner(
                    operation.operationId,
                    out IReadOnlyList<ItemQuantityLease> leases)
                || !TryCaptureOperationAuthority(
                    intent,
                    leases,
                    out string fingerprint,
                    out _,
                    out _,
                    out failureReason)
                || !string.Equals(fingerprint, operation.operationFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason = string.IsNullOrEmpty(failureReason)
                    ? "production-input-destination-drain-actor-operation-drift:"
                        + operation.operationId
                    : failureReason;
                return false;
            }
            foreach (string stackId in operation.carriedStackIds)
            {
                ProductionInputDestinationDrainStackSaveData row =
                    state.sourceStacks.SingleOrDefault(value => string.Equals(
                        value.stackId, stackId, StringComparison.Ordinal));
                if (row == null
                    || !live.TryGetValue(stackId, out WorldItemStackSnapshot stack)
                    || !StackInvariantMatches(row, stack)
                    || stack.State != WorldItemStackState.Carried
                    || stack.Position != actor.GetNowXY()
                    || !string.Equals(
                        stack.DestinationId,
                        ActorId(actor),
                        StringComparison.Ordinal))
                {
                    failureReason =
                        "production-input-destination-drain-carried-source-drift:"
                        + stackId;
                    return false;
                }
            }
        }
        return true;
    }

    private bool TryVerifyActorReleased(
        CharacterActor actor,
        IReadOnlyCollection<string> allowedOperations,
        ProductionInputDestinationCustodyDrainSaveData state,
        out string failureReason)
    {
        failureReason = string.Empty;
        AbilityHaul haul = actor.GetComponent<AbilityHaul>();
        if (haul != null && haul.CaptureActiveHaulOperationIds().Any(
                allowedOperations.Contains)
            || actor.CarryInventory?.Items.Any(value => value != null
                && value.quantity > 0
                && allowedOperations.Contains(
                    value.ownerOperationId ?? string.Empty)) == true)
        {
            failureReason =
                "production-input-destination-drain-actor-release-incomplete:"
                + ActorId(actor);
            return false;
        }
        Dictionary<string, WorldItemStackSnapshot> live = (worldItems.GetAllStacks()
                ?? Array.Empty<WorldItemStackSnapshot>())
            .Where(value => value != null && value.Quantity > 0)
            .ToDictionary(value => value.StackId, StringComparer.Ordinal);
        Vector2Int dropCell = actor.GetNowXY();
        foreach (ProductionInputDestinationDrainOperationSaveData operation in
                 state.sourceOperations.Where(value => allowedOperations.Contains(
                     value.operationId)))
        {
            foreach (string stackId in operation.carriedStackIds)
            {
                ProductionInputDestinationDrainStackSaveData row =
                    state.sourceStacks.Single(value => string.Equals(
                        value.stackId, stackId, StringComparison.Ordinal));
                if (!live.TryGetValue(stackId, out WorldItemStackSnapshot stack)
                    || !StackInvariantMatches(row, stack)
                    || stack.State != WorldItemStackState.Loose
                    || stack.Position != dropCell
                    || !string.IsNullOrEmpty(stack.DestinationId))
                {
                    failureReason =
                        "production-input-destination-drain-carried-drop-invalid:"
                        + stackId;
                    return false;
                }
            }
        }
        return true;
    }

    private bool TryCaptureFrozenStacks(
        ProductionInputDestinationCustodyDrainSaveData state,
        bool requireReleased,
        out Dictionary<string, WorldItemStackSnapshot> live,
        out string failureReason)
    {
        live = new Dictionary<string, WorldItemStackSnapshot>(
            StringComparer.Ordinal);
        failureReason = string.Empty;
        Dictionary<string, WorldItemStackSnapshot> all = (worldItems.GetAllStacks()
                ?? Array.Empty<WorldItemStackSnapshot>())
            .Where(value => value != null && value.Quantity > 0)
            .ToDictionary(value => value.StackId, StringComparer.Ordinal);
        int quantity = 0;
        long mass = 0L;
        try
        {
            foreach (ProductionInputDestinationDrainStackSaveData row in
                     state.sourceStacks)
            {
                if (!all.TryGetValue(
                        row.stackId,
                        out WorldItemStackSnapshot stack)
                    || !StackInvariantMatches(row, stack)
                    || requireReleased
                    && (stack.State is WorldItemStackState.Carried
                            or WorldItemStackState.InTransit
                        || string.Equals(
                            stack.DestinationId,
                            state.sourceDestinationId,
                            StringComparison.Ordinal)))
                {
                    failureReason =
                        "production-input-destination-drain-frozen-stack-drift:"
                        + row.stackId;
                    return false;
                }
                live.Add(row.stackId, stack);
                quantity = checked(quantity + stack.Quantity);
                mass = checked(mass + GetMass(stack));
            }
        }
        catch (Exception exception) when (exception is OverflowException
            or ArgumentException or InvalidOperationException)
        {
            failureReason =
                "production-input-destination-drain-frozen-total-invalid:"
                + exception.GetType().Name;
            live.Clear();
            return false;
        }
        if (quantity != state.inputQuantity || mass != state.inputMassGrams)
        {
            failureReason =
                "production-input-destination-drain-frozen-total-drift";
            return false;
        }
        return true;
    }

    private bool TryCaptureOperation(
        HaulDeliveryIntentSaveData intent,
        string sourceDestinationId,
        IReadOnlyDictionary<string, WorldItemStackSnapshot> stacksById,
        IReadOnlyList<CharacterActor> actors,
        ISet<string> sourceStackIds,
        out ProductionInputDestinationDrainOperationSaveData row,
        out bool requiresActor,
        out string failureReason)
    {
        row = null;
        requiresActor = false;
        failureReason = string.Empty;
        if (intent == null
            || !IsToken(intent.operationId)
            || !IsToken(intent.ownerCharacterId)
            || !string.Equals(
                intent.destinationId,
                sourceDestinationId,
                StringComparison.Ordinal)
            || !reservations.TryGetLeasesByOwner(
                intent.operationId,
                out IReadOnlyList<ItemQuantityLease> leases)
            || !TryCaptureOperationAuthority(
                intent,
                leases,
                out string operationFingerprint,
                out string[] leaseAuthorityFingerprints,
                out string[] carriedStackIds,
                out failureReason))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "production-input-destination-drain-operation-invalid"
                : failureReason;
            return false;
        }

        CharacterActor[] matches = actors.Where(value => string.Equals(
                ActorId(value),
                intent.ownerCharacterId,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length > 1)
        {
            failureReason =
                "production-input-destination-drain-actor-duplicate:"
                + intent.ownerCharacterId;
            return false;
        }
        CharacterActor actor = matches.SingleOrDefault();
        bool hasPlan = actor?.GetComponent<AbilityHaul>()?
            .OwnsHaulOperation(intent.operationId) == true;
        bool hasCargo = actor?.CarryInventory?.Items.Any(value => value != null
            && value.quantity > 0
            && string.Equals(
                value.ownerOperationId,
                intent.operationId,
                StringComparison.Ordinal)) == true;
        requiresActor = hasPlan || hasCargo;
        if (intent.HasCommittedPickup
            && (actor == null || !hasPlan || !hasCargo
                || !CommittedCarryMatches(
                    actor,
                    intent,
                    leases,
                    stacksById,
                    out failureReason)))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "production-input-destination-drain-committed-carry-missing:"
                    + intent.operationId
                : failureReason;
            return false;
        }
        foreach (string stackId in carriedStackIds)
            sourceStackIds.Add(stackId);

        row = new ProductionInputDestinationDrainOperationSaveData
        {
            operationId = intent.operationId,
            actorId = intent.ownerCharacterId,
            hadCommittedPickup = intent.HasCommittedPickup,
            operationFingerprint = operationFingerprint,
            leaseAuthorityFingerprints = leaseAuthorityFingerprints.ToList(),
            carriedStackIds = carriedStackIds.ToList()
        };
        return true;
    }

    private bool TryCaptureOperationAuthority(
        HaulDeliveryIntentSaveData intent,
        IReadOnlyList<ItemQuantityLease> leases,
        out string operationFingerprint,
        out string[] leaseAuthorityFingerprints,
        out string[] carriedStackIds,
        out string failureReason)
    {
        operationFingerprint = string.Empty;
        leaseAuthorityFingerprints = Array.Empty<string>();
        carriedStackIds = Array.Empty<string>();
        failureReason = string.Empty;
        if (intent == null
            || !IsToken(intent.operationId)
            || !IsToken(intent.ownerCharacterId)
            || !IsToken(intent.destinationId)
            || intent.commitments == null
            || intent.warehouseAdmissions == null)
        {
            failureReason =
                "production-input-destination-drain-intent-invalid";
            return false;
        }
        ItemQuantityLease[] orderedLeases = (leases
                ?? Array.Empty<ItemQuantityLease>())
            .Where(value => value != null)
            .OrderBy(CreateStableLeaseAuthorityFingerprint,
                StringComparer.Ordinal)
            .ToArray();
        if (orderedLeases.Length != (leases?.Count ?? 0)
            || orderedLeases.Any(value => !string.Equals(
                    value.ownerOperationId,
                    intent.operationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    value.ownerCharacterId,
                    intent.ownerCharacterId,
                    StringComparison.Ordinal)
                || value.purpose != ItemReservationPurpose.Hauling
                || value.originalQuantity <= 0
                || value.remainingQuantity <= 0
                || value.slices == null
                || value.slices.Count == 0
                || value.slices.Any(slice => slice == null
                    || !IsToken(slice.stackId)
                    || !IsToken(slice.originStackId)
                    || !IsToken(slice.expectedStackSignature)
                    || slice.quantity <= 0)
                || !TrySumSliceQuantity(
                    value.slices,
                    out int sliceQuantity)
                || sliceQuantity != value.remainingQuantity))
        {
            failureReason =
                "production-input-destination-drain-lease-owner-invalid:"
                + intent.operationId;
            return false;
        }
        leaseAuthorityFingerprints = orderedLeases
            .Select(CreateStableLeaseAuthorityFingerprint)
            .ToArray();
        if (leaseAuthorityFingerprints.Distinct(StringComparer.Ordinal).Count()
            != leaseAuthorityFingerprints.Length)
        {
            failureReason =
                "production-input-destination-drain-lease-authority-duplicate:"
                + intent.operationId;
            return false;
        }
        carriedStackIds = intent.commitments
            .Where(value => value != null && value.quantity > 0)
            .Select(value => value.carriedStackId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (carriedStackIds.Any(value => !IsToken(value))
            || carriedStackIds.Distinct(StringComparer.Ordinal).Count()
                != carriedStackIds.Length)
        {
            failureReason =
                "production-input-destination-drain-commitment-invalid:"
                + intent.operationId;
            return false;
        }

        StringBuilder canonical = new StringBuilder(512)
            .Append("production-input-destination-operation@1|");
        Append(canonical, intent.operationId);
        Append(canonical, intent.ownerCharacterId);
        canonical.Append((int)intent.destinationKind).Append('|');
        Append(canonical, intent.destinationId);
        canonical.Append(intent.deliveryGridX).Append('|')
            .Append(intent.deliveryGridY).Append('|')
            .Append(intent.dropGridX).Append('|')
            .Append(intent.dropGridY).Append('|');
        foreach (HaulDeliveryItemCommitmentSaveData commitment in
                 intent.commitments.OrderBy(
                     value => value?.carriedStackId,
                     StringComparer.Ordinal))
        {
            if (commitment == null
                || !IsToken(commitment.carriedStackId)
                || !IsToken(commitment.sourceStackId)
                || !IsToken(commitment.itemId)
                || !IsToken(commitment.expectedStackSignature)
                || commitment.quantity <= 0)
            {
                failureReason =
                    "production-input-destination-drain-commitment-invalid:"
                    + intent.operationId;
                return false;
            }
            Append(canonical, commitment.carriedStackId);
            Append(canonical, commitment.sourceStackId);
            Append(canonical, commitment.itemId);
            Append(canonical, commitment.expectedStackSignature);
            canonical.Append(commitment.quantity).Append('|');
        }
        canonical.Append("admissions|");
        foreach (WarehouseHaulAdmissionSaveData admission in
                 intent.warehouseAdmissions.OrderBy(
                     value => value?.tokenId,
                     StringComparer.Ordinal))
        {
            if (admission == null)
            {
                failureReason =
                    "production-input-destination-drain-admission-invalid:"
                    + intent.operationId;
                return false;
            }
            Append(canonical, admission.tokenId);
            Append(canonical, admission.ownerAdmissionOperationId);
            Append(canonical, admission.warehouseId);
            Append(canonical, admission.sourceWarehouseId);
            Append(canonical, admission.sourceStackId);
            Append(canonical, admission.itemId);
            Append(canonical, admission.itemInstanceId);
            Append(canonical, admission.lotFingerprint);
            canonical.Append(admission.quantity).Append('|')
                .Append(admission.reservedMassGrams).Append('|')
                .Append(admission.catalogRevision).Append('|')
                .Append(admission.sourceRevision).Append('|');
        }
        canonical.Append("leases|");
        foreach (string leaseFingerprint in leaseAuthorityFingerprints)
            Append(canonical, leaseFingerprint);
        operationFingerprint =
            ProductionInputDestinationCustodyDrainFingerprint.Hash(
                canonical.ToString());
        return true;
    }

    private static string CreateStableLeaseAuthorityFingerprint(
        ItemQuantityLease lease)
    {
        if (lease == null)
            return string.Empty;
        StringBuilder canonical = new StringBuilder(256)
            .Append("production-input-destination-lease-authority@1|");
        Append(canonical, lease.ownerOperationId);
        Append(canonical, lease.ownerCharacterId);
        canonical.Append((int)lease.purpose).Append('|');
        Append(canonical, lease.aggregationCohortId);
        canonical.Append(lease.originalQuantity).Append('|')
            .Append(lease.remainingQuantity).Append('|');
        foreach (ItemLeaseSlice slice in (lease.slices
                     ?? new List<ItemLeaseSlice>())
                 .OrderBy(value => value?.originStackId, StringComparer.Ordinal)
                 .ThenBy(value => value?.stackId, StringComparer.Ordinal)
                 .ThenBy(value => value?.expectedStackSignature,
                     StringComparer.Ordinal)
                 .ThenBy(value => value?.quantity ?? 0))
        {
            Append(canonical, slice?.originStackId);
            Append(canonical, slice?.stackId);
            Append(canonical, slice?.expectedStackSignature);
            canonical.Append(slice?.quantity ?? -1).Append('|');
        }
        return ProductionInputDestinationCustodyDrainFingerprint.Hash(
            canonical.ToString());
    }

    private bool TryCaptureStack(
        WorldItemStackSnapshot snapshot,
        out ProductionInputDestinationDrainStackSaveData row,
        out string failureReason)
    {
        row = null;
        failureReason = string.Empty;
        if (snapshot == null
            || !IsToken(snapshot.StackId)
            || !IsToken(snapshot.ItemId)
            || snapshot.Quantity <= 0
            || snapshot.ReservationRevision < 0L)
        {
            failureReason =
                "production-input-destination-drain-stack-invalid";
            return false;
        }
        try
        {
            row = new ProductionInputDestinationDrainStackSaveData
            {
                stackId = snapshot.StackId,
                itemId = snapshot.ItemId,
                itemInstanceId = snapshot.ItemInstanceId ?? string.Empty,
                componentFingerprint = CreateComponentFingerprint(snapshot),
                quantity = snapshot.Quantity,
                massGrams = GetMass(snapshot),
                state = snapshot.State,
                positionX = snapshot.Position.x,
                positionY = snapshot.Position.y,
                sourceStorageDestinationId =
                    snapshot.SourceStorageDestinationId ?? string.Empty,
                destinationPositionX = snapshot.DestinationPosition.x,
                destinationPositionY = snapshot.DestinationPosition.y,
                reservationRevision = snapshot.ReservationRevision
            };
            return row.massGrams > 0L;
        }
        catch (Exception exception)
        {
            failureReason =
                "production-input-destination-drain-stack-mass-invalid:"
                + snapshot.StackId + ":" + exception.GetType().Name;
            row = null;
            return false;
        }
    }

    private bool CommittedCarryMatches(
        CharacterActor actor,
        HaulDeliveryIntentSaveData intent,
        IReadOnlyList<ItemQuantityLease> leases,
        IReadOnlyDictionary<string, WorldItemStackSnapshot> stacksById,
        out string failureReason)
    {
        failureReason = string.Empty;
        CharacterCarriedItemSaveData[] carried = actor.CarryInventory.Items
            .Where(value => value != null && value.quantity > 0
                && string.Equals(
                    value.ownerOperationId,
                    intent.operationId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ToArray();
        HaulDeliveryItemCommitmentSaveData[] commitments = intent.commitments
            .Where(value => value != null && value.quantity > 0)
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ToArray();
        if (carried.Length != commitments.Length
            || commitments.Length != intent.commitments.Count)
        {
            failureReason =
                "production-input-destination-drain-carry-cardinality-conflict:"
                + intent.operationId;
            return false;
        }
        for (int index = 0; index < commitments.Length; index++)
        {
            HaulDeliveryItemCommitmentSaveData commitment = commitments[index];
            CharacterCarriedItemSaveData item = carried[index];
            int leased;
            try
            {
                leased = (leases ?? Array.Empty<ItemQuantityLease>())
                    .Where(value => value != null)
                    .SelectMany(value => value.slices
                        ?? new List<ItemLeaseSlice>())
                    .Where(value => value != null
                        && string.Equals(
                            value.stackId,
                            commitment.carriedStackId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.expectedStackSignature,
                            commitment.expectedStackSignature,
                            StringComparison.Ordinal))
                    .Aggregate(0, (current, value) => checked(
                        current + value.quantity));
            }
            catch (OverflowException)
            {
                failureReason =
                    "production-input-destination-drain-carry-quantity-overflow:"
                    + commitment.carriedStackId;
                return false;
            }
            if (!string.Equals(item.carriedStackId,
                    commitment.carriedStackId, StringComparison.Ordinal)
                || !string.Equals(item.sourceStackId,
                    commitment.sourceStackId, StringComparison.Ordinal)
                || !string.Equals(item.itemId,
                    commitment.itemId, StringComparison.Ordinal)
                || item.quantity != commitment.quantity
                || leased != commitment.quantity
                || !string.Equals(
                    ItemReservationSignature.Create(
                        item.itemId,
                        item.components),
                    commitment.expectedStackSignature,
                    StringComparison.Ordinal)
                || !stacksById.TryGetValue(
                    commitment.carriedStackId,
                    out WorldItemStackSnapshot stack)
                || stack.State != WorldItemStackState.Carried
                || stack.Quantity != commitment.quantity
                || !string.Equals(stack.ItemId,
                    commitment.itemId, StringComparison.Ordinal)
                || !string.Equals(stack.ItemInstanceId ?? string.Empty,
                    item.itemInstanceId ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(stack.DestinationId,
                    intent.ownerCharacterId, StringComparison.Ordinal)
                || !string.Equals(stack.ReservationSignature,
                    commitment.expectedStackSignature, StringComparison.Ordinal))
            {
                failureReason =
                    "production-input-destination-drain-carry-join-conflict:"
                    + commitment.carriedStackId;
                return false;
            }
        }
        return true;
    }

    private bool TryValidateActorClosure(
        CharacterActor actor,
        IEnumerable<string> allowedOperationIds,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || actor.CarryInventory == null)
        {
            failureReason =
                "production-input-destination-drain-actor-authority-missing";
            return false;
        }
        AbilityHaul haul = actor.GetComponent<AbilityHaul>();
        if (haul == null)
        {
            failureReason =
                "production-input-destination-drain-actor-haul-missing:"
                + ActorId(actor);
            return false;
        }
        HashSet<string> allowed = (allowedOperationIds ?? Array.Empty<string>())
            .ToHashSet(StringComparer.Ordinal);
        string foreignPlan = haul.CaptureActiveHaulOperationIds()
            .FirstOrDefault(value => !allowed.Contains(value));
        string foreignCargo = actor.CarryInventory.Items
            .Where(value => value != null && value.quantity > 0)
            .Select(value => value.ownerOperationId ?? string.Empty)
            .FirstOrDefault(value => !allowed.Contains(value));
        if (!string.IsNullOrEmpty(foreignPlan)
            || !string.IsNullOrEmpty(foreignCargo))
        {
            failureReason =
                "production-input-destination-drain-actor-mixed-authority:"
                + ActorId(actor) + ":"
                + (foreignPlan ?? foreignCargo);
            return false;
        }
        return true;
    }

    private string CreateActorFingerprint(
        CharacterActor actor,
        IEnumerable<string> allowedOperationIds)
    {
        StringBuilder canonical = new StringBuilder(256)
            .Append("production-input-destination-actor@1|");
        Append(canonical, ActorId(actor));
        foreach (string operationId in actor.GetComponent<AbilityHaul>()
                     .CaptureActiveHaulOperationIds()
                     .Where((allowedOperationIds ?? Array.Empty<string>())
                         .Contains)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            Append(canonical, operationId);
        }
        canonical.Append("cargo|");
        foreach (CharacterCarriedItemSaveData item in actor.CarryInventory.Items
                     .Where(value => value != null && value.quantity > 0
                         && (allowedOperationIds ?? Array.Empty<string>())
                         .Contains(value.ownerOperationId))
                     .OrderBy(value => value.ownerOperationId,
                         StringComparer.Ordinal)
                     .ThenBy(value => value.carriedStackId,
                         StringComparer.Ordinal))
        {
            Append(canonical, item.ownerOperationId);
            Append(canonical, item.carriedStackId);
            Append(canonical, item.sourceStackId);
            Append(canonical, item.itemId);
            Append(canonical, item.itemInstanceId);
            canonical.Append(item.quantity).Append('|');
            Append(canonical, ItemReservationSignature.Create(
                item.itemId, item.components));
        }
        return ProductionInputDestinationCustodyDrainFingerprint.Hash(
            canonical.ToString());
    }

    private bool StackInvariantMatches(
        ProductionInputDestinationDrainStackSaveData expected,
        WorldItemStackSnapshot actual) => actual != null
        && actual.Quantity == expected.quantity
        && string.Equals(actual.StackId, expected.stackId,
            StringComparison.Ordinal)
        && string.Equals(actual.ItemId, expected.itemId,
            StringComparison.Ordinal)
        && string.Equals(actual.ItemInstanceId ?? string.Empty,
            expected.itemInstanceId, StringComparison.Ordinal)
        && string.Equals(CreateComponentFingerprint(actual),
            expected.componentFingerprint, StringComparison.Ordinal)
        && GetMass(actual) == expected.massGrams;

    private static bool StrictStackLocationMatches(
        ProductionInputDestinationCustodyDrainSaveData state,
        ProductionInputDestinationDrainStackSaveData expected,
        WorldItemStackSnapshot actual,
        bool allowReservationRevisionAdvance = false)
    {
        if (actual.State != expected.state
            || (allowReservationRevisionAdvance
                ? actual.ReservationRevision < expected.reservationRevision
                : actual.ReservationRevision != expected.reservationRevision)
            || actual.DestinationPosition != new Vector2Int(
                expected.destinationPositionX,
                expected.destinationPositionY)
            || !string.Equals(
                actual.SourceStorageDestinationId ?? string.Empty,
                expected.sourceStorageDestinationId,
                StringComparison.Ordinal))
        {
            return false;
        }
        if (expected.state == WorldItemStackState.Carried)
        {
            ProductionInputDestinationDrainOperationSaveData operation =
                state.sourceOperations.SingleOrDefault(value =>
                    value.carriedStackIds.Contains(expected.stackId));
            return operation != null
                && string.Equals(actual.DestinationId,
                    operation.actorId, StringComparison.Ordinal);
        }
        return actual.Position == new Vector2Int(
                expected.positionX,
                expected.positionY)
            && string.Equals(actual.DestinationId,
                state.sourceDestinationId, StringComparison.Ordinal);
    }

    private bool TryFindActor(
        string actorId,
        out CharacterActor actor,
        out string failureReason)
    {
        actor = null;
        failureReason = string.Empty;
        CharacterActor[] matches = (characterWorld.Characters
                ?? Array.Empty<CharacterActor>())
            .Where(value => value != null
                && string.Equals(ActorId(value), actorId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            failureReason = matches.Length == 0
                ? "production-input-destination-drain-actor-missing:" + actorId
                : "production-input-destination-drain-actor-duplicate:" + actorId;
            return false;
        }
        actor = matches[0];
        return true;
    }

    private Dictionary<string, CharacterActor> CurrentActorsById(
        out string failureReason)
    {
        failureReason = string.Empty;
        CharacterActor[] actors = (characterWorld.Characters
                ?? Array.Empty<CharacterActor>())
            .Where(value => value != null)
            .ToArray();
        if (actors.Any(value => !IsToken(ActorId(value)))
            || actors.Select(ActorId).Distinct(StringComparer.Ordinal).Count()
                != actors.Length)
        {
            failureReason =
                "production-input-destination-drain-actor-authority-invalid";
            return null;
        }
        return actors.ToDictionary(ActorId, StringComparer.Ordinal);
    }

    private bool AnyActorOwnsOrCarries(string operationId) =>
        (characterWorld.Characters ?? Array.Empty<CharacterActor>())
        .Where(value => value != null)
        .Any(value => value.GetComponent<AbilityHaul>()?
                .OwnsHaulOperation(operationId) == true
            || value.CarryInventory?.Items.Any(item => item != null
                && item.quantity > 0
                && string.Equals(item.ownerOperationId,
                    operationId, StringComparison.Ordinal)) == true);

    private bool HasAnyOperationAuthority(string operationId) =>
        worldItems.TryCaptureHaulDeliveryIntent(operationId, out _)
        || reservations.TryGetLeasesByOwner(
            operationId,
            out IReadOnlyList<ItemQuantityLease> leases)
            && leases != null && leases.Count > 0
        || AnyActorOwnsOrCarries(operationId);

    private bool HasForeignLease(
        string stackId,
        IEnumerable<string> allowedOperationIds)
    {
        HashSet<string> allowed = (allowedOperationIds
                ?? Array.Empty<string>())
            .ToHashSet(StringComparer.Ordinal);
        return (reservations.GetLeasesForStack((ItemStackId)stackId)
                ?? Array.Empty<ItemQuantityLease>())
            .Any(value => value == null
                || !allowed.Contains(value.ownerOperationId));
    }

    private bool ReservationLedgerMatches(WorldItemStackSnapshot stack)
    {
        if (stack == null || !IsToken(stack.StackId))
            return false;
        int reserved;
        try
        {
            reserved = (reservations.GetLeasesForStack(
                        (ItemStackId)stack.StackId)
                    ?? Array.Empty<ItemQuantityLease>())
                .Where(value => value != null)
                .SelectMany(value => value.slices ?? new List<ItemLeaseSlice>())
                .Where(value => value != null && string.Equals(
                    value.stackId,
                    stack.StackId,
                    StringComparison.Ordinal))
                .Sum(value => checked(value.quantity));
        }
        catch (OverflowException)
        {
            return false;
        }
        return reserved == stack.ReservedQuantity;
    }

    private static bool TrySumSliceQuantity(
        IEnumerable<ItemLeaseSlice> slices,
        out int quantity)
    {
        quantity = 0;
        try
        {
            foreach (ItemLeaseSlice slice in slices
                         ?? Array.Empty<ItemLeaseSlice>())
                quantity = checked(quantity + (slice?.quantity ?? 0));
            return true;
        }
        catch (OverflowException)
        {
            quantity = 0;
            return false;
        }
    }

    private bool TryValidateReservationClosure(
        ISet<string> sourceStackIds,
        IEnumerable<string> sourceOperationIds,
        IReadOnlyDictionary<string, WorldItemStackSnapshot> stacksById,
        out string failureReason)
    {
        failureReason = string.Empty;
        HashSet<string> sourceOperations = (sourceOperationIds
                ?? Array.Empty<string>())
            .ToHashSet(StringComparer.Ordinal);
        foreach (string stackId in sourceStackIds)
        {
            if (!stacksById.TryGetValue(
                    stackId,
                    out WorldItemStackSnapshot stack)
                || !ReservationLedgerMatches(stack))
            {
                failureReason =
                    "production-input-destination-drain-reservation-ledger-conflict:"
                    + stackId;
                return false;
            }
            ItemQuantityLease[] stackLeases = (reservations.GetLeasesForStack(
                        (ItemStackId)stackId)
                    ?? Array.Empty<ItemQuantityLease>())
                .Where(value => value != null)
                .ToArray();
            if (stackLeases.Any(value =>
                    !sourceOperations.Contains(value.ownerOperationId)))
            {
                failureReason =
                    "production-input-destination-drain-external-lease:"
                    + stackId;
                return false;
            }
        }
        foreach (string operationId in sourceOperations)
        {
            if (!reservations.TryGetLeasesByOwner(
                    operationId,
                    out IReadOnlyList<ItemQuantityLease> leases))
            {
                continue;
            }
            string outside = leases.Where(value => value != null)
                .SelectMany(value => value.slices ?? new List<ItemLeaseSlice>())
                .Where(value => value != null)
                .Select(value => value.stackId)
                .FirstOrDefault(value => !sourceStackIds.Contains(value));
            if (!string.IsNullOrEmpty(outside))
            {
                failureReason =
                    "production-input-destination-drain-lease-outside-source:"
                    + operationId + ":" + outside;
                return false;
            }
        }
        return true;
    }

    private long GetMass(WorldItemStackSnapshot stack)
    {
        ItemDefinitionId itemId = (ItemDefinitionId)stack.ItemId;
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            itemId,
            stack.ItemInstanceId,
            stack.Components);
        return massQuery.GetQuantityMass(
            itemId,
            subject,
            stack.Quantity).Value;
    }

    private static string CreateComponentFingerprint(
        WorldItemStackSnapshot stack) =>
        ProductionInputDestinationCustodyDrainFingerprint.Hash(
            ItemReservationSignature.Create(stack.ItemId, stack.Components));

    private static string CreateOwnershipFingerprint(
        string sourceDestinationId,
        long massAuthorityRevision,
        IEnumerable<ProductionInputDestinationDrainStackSaveData> stacks,
        IEnumerable<ProductionInputDestinationDrainOperationSaveData> operations,
        IEnumerable<ProductionInputDestinationDrainActorSaveData> actors,
        int quantity,
        long massGrams)
    {
        StringBuilder canonical = new StringBuilder(512)
            .Append("production-input-destination-ownership@1|");
        Append(canonical, sourceDestinationId);
        canonical.Append(massAuthorityRevision).Append('|')
            .Append(quantity).Append('|').Append(massGrams).Append('|');
        foreach (ProductionInputDestinationDrainStackSaveData value in stacks
                     .OrderBy(value => value.stackId, StringComparer.Ordinal))
        {
            Append(canonical, value.stackId);
            Append(canonical, value.itemId);
            Append(canonical, value.itemInstanceId);
            Append(canonical, value.componentFingerprint);
            canonical.Append(value.quantity).Append('|')
                .Append(value.massGrams).Append('|')
                .Append((int)value.state).Append('|')
                .Append(value.positionX).Append('|')
                .Append(value.positionY).Append('|');
            Append(canonical, value.sourceStorageDestinationId);
            canonical.Append(value.destinationPositionX).Append('|')
                .Append(value.destinationPositionY).Append('|')
                .Append(value.reservationRevision).Append('|');
        }
        canonical.Append("operations|");
        foreach (ProductionInputDestinationDrainOperationSaveData value in operations
                     .OrderBy(value => value.operationId, StringComparer.Ordinal))
        {
            Append(canonical, value.operationId);
            Append(canonical, value.actorId);
            canonical.Append(value.hadCommittedPickup ? '1' : '0').Append('|');
            Append(canonical, value.operationFingerprint);
            foreach (string lease in value.leaseAuthorityFingerprints)
                Append(canonical, lease);
            foreach (string stack in value.carriedStackIds)
                Append(canonical, stack);
        }
        canonical.Append("actors|");
        foreach (ProductionInputDestinationDrainActorSaveData value in actors
                     .OrderBy(value => value.actorId, StringComparer.Ordinal))
        {
            Append(canonical, value.actorId);
            Append(canonical, value.sourcePhysicalFingerprint);
            foreach (string operation in value.allowedOperationIds)
                Append(canonical, operation);
        }
        return ProductionInputDestinationCustodyDrainFingerprint.Hash(
            canonical.ToString());
    }

    private string CreateResultFingerprint(
        ProductionInputDestinationCustodyDrainSaveData state,
        IReadOnlyDictionary<string, WorldItemStackSnapshot> stacks)
    {
        StringBuilder canonical = new StringBuilder(384)
            .Append("production-input-destination-drain-result@1|");
        Append(canonical, state.stepOperationId);
        Append(canonical, state.requestFingerprint);
        foreach (KeyValuePair<string, WorldItemStackSnapshot> pair in stacks
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            WorldItemStackSnapshot stack = pair.Value;
            Append(canonical, stack.StackId);
            Append(canonical, stack.ItemId);
            Append(canonical, stack.ItemInstanceId);
            Append(canonical, CreateComponentFingerprint(stack));
            canonical.Append(stack.Quantity).Append('|')
                .Append(GetMass(stack)).Append('|')
                .Append((int)stack.State).Append('|')
                .Append(stack.Position.x).Append('|')
                .Append(stack.Position.y).Append('|');
            Append(canonical, stack.DestinationId);
            Append(canonical, stack.SourceStorageDestinationId);
            canonical.Append(stack.ReservationRevision).Append('|');
        }
        return ProductionInputDestinationCustodyDrainFingerprint.Hash(
            canonical.ToString());
    }

    private static string ActorId(CharacterActor actor) =>
        actor?.BuildingCharacterId.Value ?? string.Empty;

    private static void Append(StringBuilder target, string value)
    {
        string token = value ?? string.Empty;
        target.Append(token.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(token).Append('|');
    }

    private static bool IsToken(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsDigest(string value) => value?.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private static ProductionInputDestinationCustodyDrainResult Current(
        ProductionInputDestinationCustodyDrainSaveData state,
        ProductionInputDestinationCustodyDrainStatus status) => new(
        status,
        state.commitId,
        state.receiptFingerprint,
        string.Empty);

    private static ProductionInputDestinationCustodyDrainResult Deferred(
        string reason) => new(
        ProductionInputDestinationCustodyDrainStatus.Deferred,
        string.Empty,
        string.Empty,
        reason);

    private static ProductionInputDestinationCustodyDrainResult Conflict(
        string reason) => new(
        ProductionInputDestinationCustodyDrainStatus.Conflict,
        string.Empty,
        string.Empty,
        reason);
}
