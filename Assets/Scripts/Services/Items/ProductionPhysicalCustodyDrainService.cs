using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Items-owned executor for one journal-planned production-output destination
/// drain. The producer outbox is the durable progress authority. Every commit
/// call advances at most one actor, one intent, one phase, or the final
/// destination effect so a crash can resume without reapplying earlier work.
/// </summary>
public sealed class ProductionPhysicalCustodyDrainService :
    IProductionPhysicalCustodyDrainPort
{
    private const string PhysicalContributorId =
        "physical-custody-carry-recovery";
    private const string InterruptionReason =
        "production-output-destination-destructive-drain";

    private readonly WorldItemRepository repository;
    private readonly IProductionPhysicalCustodyDrainOutbox outbox;
    private readonly ICharacterLifetimeQuery characterLifetime;
    private readonly IItemQuantityReservationService reservations;
    private readonly IItemTransferService transfers;
    private readonly IWorldItemStackRuntime worldItems;
    private readonly IFacilityOutputExactRouteOutboxQuery exactRoutes;
    private readonly IItemReservationMutationGate mutationGate;
    private readonly IProductionOutputDestinationLifecycleQuery lifecycle;

    public ProductionPhysicalCustodyDrainService(
        WorldItemRepository repository,
        IProductionPhysicalCustodyDrainOutbox outbox,
        ICharacterLifetimeQuery characterLifetime,
        IItemQuantityReservationService reservations,
        IItemTransferService transfers,
        IWorldItemStackRuntime worldItems,
        IFacilityOutputExactRouteOutboxQuery exactRoutes,
        IItemReservationMutationGate mutationGate,
        IProductionOutputDestinationLifecycleQuery lifecycle)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        this.characterLifetime = characterLifetime
            ?? throw new ArgumentNullException(nameof(characterLifetime));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.transfers = transfers
            ?? throw new ArgumentNullException(nameof(transfers));
        this.worldItems = worldItems
            ?? throw new ArgumentNullException(nameof(worldItems));
        this.exactRoutes = exactRoutes
            ?? throw new ArgumentNullException(nameof(exactRoutes));
        this.mutationGate = mutationGate
            ?? throw new ArgumentNullException(nameof(mutationGate));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
    }

    public bool TryCaptureRequest(
        string stepOperationId,
        string ownerStableId,
        string sourceDestinationId,
        int ownerGridX,
        int ownerGridY,
        string expectedSourceOwnershipFingerprint,
        out ProductionPhysicalCustodyDrainRequest request,
        out string failureReason)
    {
        request = null;
        failureReason = string.Empty;
        if (!IsToken(stepOperationId)
            || !ProductionOutputDestinationId.TryParse(
                sourceDestinationId,
                out ProductionOutputDestinationId destination)
            || !string.Equals(
                ownerStableId,
                "physical-destination:" + destination.Value,
                StringComparison.Ordinal)
            || !IsDigest(expectedSourceOwnershipFingerprint))
        {
            failureReason = "production-physical-custody-capture-identity-invalid";
            return false;
        }

        BuildingInstanceId facilityId = (BuildingInstanceId)
            destination.Value.Substring(ProductionOutputDestinationId.Prefix.Length);
        ProductionOutputDestinationLifecycleSnapshot snapshot;
        try
        {
            snapshot = lifecycle.Capture(facilityId);
        }
        catch (Exception exception)
        {
            failureReason = "production-physical-custody-lifecycle-capture-failed:"
                + exception.GetType().Name;
            return false;
        }

        ProductionOutputDestinationLifecycleContribution[] physical = snapshot
            .Contributions
            .Where(value => value != null
                && string.Equals(
                    value.ContributorId,
                    PhysicalContributorId,
                    StringComparison.Ordinal))
            .ToArray();
        if (physical.Length != 1
            || !string.Equals(
                physical[0].DurableSemanticFingerprint,
                expectedSourceOwnershipFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "production-physical-custody-source-fingerprint-conflict";
            return false;
        }

        WorldItemStackRecord[] sourceRecords = repository.Records
            .Where(record => IsSourceRecord(record, destination.Value))
            .OrderBy(record => record.stackId, StringComparer.Ordinal)
            .ToArray();
        if (sourceRecords.Length == 0)
        {
            failureReason = "production-physical-custody-source-empty";
            return false;
        }

        HashSet<string> sourceStackIds = sourceRecords
            .Select(record => record.stackId)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, HaulDeliveryIntentSaveData> intents =
            new(StringComparer.Ordinal);
        Dictionary<string, ItemQuantityLease> sourceLeases =
            new(StringComparer.Ordinal);
        foreach (WorldItemStackRecord sourceRecord in sourceRecords)
        {
            ItemQuantityLease[] stackLeases = (reservations.GetLeasesForStack(
                    (ItemStackId)sourceRecord.stackId)
                    ?? Array.Empty<ItemQuantityLease>())
                .Where(value => value != null)
                .ToArray();
            int leasedQuantity;
            try
            {
                leasedQuantity = stackLeases.Sum(lease => checked((lease.slices
                        ?? new List<ItemLeaseSlice>())
                    .Where(slice => slice != null
                        && string.Equals(
                            slice.stackId,
                            sourceRecord.stackId,
                            StringComparison.Ordinal))
                    .Sum(slice => checked(slice.quantity))));
            }
            catch (OverflowException)
            {
                failureReason =
                    "production-physical-custody-source-lease-overflow:"
                    + sourceRecord.stackId;
                return false;
            }
            if (leasedQuantity != sourceRecord.reservedQuantity)
            {
                failureReason =
                    "production-physical-custody-source-reservation-ledger-conflict:"
                    + sourceRecord.stackId;
                return false;
            }

            foreach (ItemQuantityLease lease in stackLeases)
            {
                if (lease.purpose != ItemReservationPurpose.Hauling)
                {
                    failureReason =
                        "production-physical-custody-external-claim-blocked:"
                        + lease.purpose + ":" + lease.ownerOperationId;
                    return false;
                }
                if (!IsToken(lease.leaseId)
                    || !IsToken(lease.ownerOperationId)
                    || !IsToken(lease.ownerCharacterId)
                    || lease.remainingQuantity <= 0
                    || lease.slices == null
                    || lease.slices.Count == 0
                    || lease.slices.Any(slice => slice == null
                        || slice.quantity <= 0
                        || !sourceStackIds.Contains(slice.stackId)
                        || !repository.RecordsById.TryGetValue(
                            slice.stackId,
                            out WorldItemStackRecord leasedRecord)
                        || !string.Equals(
                            ItemReservationSignature.Create(
                                leasedRecord.itemId,
                                leasedRecord.components),
                            slice.expectedStackSignature,
                            StringComparison.Ordinal))
                    || lease.slices.Sum(slice => slice.quantity)
                        != lease.remainingQuantity)
                {
                    failureReason =
                        "production-physical-custody-source-haul-lease-invalid:"
                        + lease.ownerOperationId;
                    return false;
                }
                if (!sourceLeases.TryAdd(lease.leaseId, lease)
                    && !string.Equals(
                        sourceLeases[lease.leaseId].ownerOperationId,
                        lease.ownerOperationId,
                        StringComparison.Ordinal))
                {
                    failureReason =
                        "production-physical-custody-source-haul-lease-duplicate:"
                        + lease.leaseId;
                    return false;
                }
            }
        }
        foreach (IGrouping<string, ItemQuantityLease> ownerGroup in sourceLeases
                     .Values.GroupBy(
                         value => value.ownerOperationId,
                         StringComparer.Ordinal))
        {
            string ownerOperationId = ownerGroup.Key;
            if (!reservations.TryGetLeasesByOwner(
                    ownerOperationId,
                    out IReadOnlyList<ItemQuantityLease> ownerLeases)
                || ownerLeases == null
                || !ownerLeases.Where(value => value != null)
                    .Select(value => value.leaseId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(
                        ownerGroup.Select(value => value.leaseId)
                            .OrderBy(value => value, StringComparer.Ordinal),
                        StringComparer.Ordinal)
                || !worldItems.TryCaptureHaulDeliveryIntent(
                    ownerOperationId,
                    out HaulDeliveryIntentSaveData leasedIntent)
                || leasedIntent == null
                || !string.Equals(
                    leasedIntent.ownerCharacterId,
                    ownerGroup.First().ownerCharacterId,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "production-physical-custody-source-lease-closure-conflict:"
                    + ownerOperationId;
                return false;
            }
            if (!TryAddIntent(leasedIntent, intents, out failureReason))
                return false;
        }
        foreach (HaulDeliveryIntentSaveData intent in repository
                     .HaulDeliveryIntents.CaptureCommitted())
        {
            if (intent?.commitments?.Any(commitment => commitment != null
                    && sourceStackIds.Contains(
                        commitment.carriedStackId ?? string.Empty)) != true)
            {
                continue;
            }
            if (!TryAddIntent(intent, intents, out failureReason))
                return false;
        }

        foreach (HaulDeliveryIntentSaveData intent in intents.Values)
        {
            if (!HaulDeliveryOperationIdentity.TryParse(
                    intent.operationId,
                    intent.ownerCharacterId,
                    out _))
            {
                failureReason =
                    "production-physical-custody-haul-identity-invalid:"
                    + intent.operationId;
                return false;
            }
            if (intent.HasCommittedPickup
                && intent.commitments.Any(commitment => commitment == null
                    || !sourceStackIds.Contains(
                        commitment.carriedStackId ?? string.Empty)))
            {
                failureReason =
                    "production-physical-custody-mixed-commitment:"
                    + intent.operationId;
                return false;
            }
        }

        int inputQuantity = 0;
        long inputMassGrams = 0L;
        try
        {
            foreach (WorldItemStackRecord record in sourceRecords)
            {
                inputQuantity = checked(inputQuantity + record.quantity);
                long recordMass = GetMass(record);
                if (FacilityOutputExactRouteCustodyCodec.TryRead(
                        record.components,
                        out FacilityOutputExactRouteCustodyMetadata custody)
                    && string.Equals(
                        custody.OriginDestinationId,
                        destination.Value,
                        StringComparison.Ordinal)
                    && custody.MassGrams != recordMass)
                {
                    failureReason =
                        "production-physical-custody-route-mass-conflict:"
                        + record.stackId;
                    return false;
                }
                inputMassGrams = checked(inputMassGrams + recordMass);
            }
        }
        catch (Exception exception) when (exception is OverflowException
            or ArgumentException or InvalidOperationException)
        {
            failureReason = "production-physical-custody-mass-capture-failed:"
                + exception.GetType().Name;
            return false;
        }

        string[] stacks = sourceStackIds.OrderBy(
            value => value,
            StringComparer.Ordinal).ToArray();
        string[] operations = intents.Keys.OrderBy(
            value => value,
            StringComparer.Ordinal).ToArray();
        string[] actors = intents.Values
            .Select(value => value.ownerCharacterId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string requestFingerprint =
            ProductionPhysicalCustodyDrainFingerprint.CreateRequest(
                stepOperationId,
                ownerStableId,
                destination.Value,
                ownerGridX,
                ownerGridY,
                expectedSourceOwnershipFingerprint,
                stacks,
                actors,
                operations,
                inputQuantity,
                inputMassGrams);
        request = new ProductionPhysicalCustodyDrainRequest(
            stepOperationId,
            ownerStableId,
            destination.Value,
            ownerGridX,
            ownerGridY,
            requestFingerprint,
            expectedSourceOwnershipFingerprint,
            stacks,
            actors,
            operations,
            inputQuantity,
            inputMassGrams);
        return true;
    }

    public ProductionPhysicalCustodyDrainResult TryPrepare(
        ProductionPhysicalCustodyDrainRequest request) => outbox.TryPrepare(request);

    public ProductionPhysicalCustodyDrainResult TryCommit(
        string stepOperationId,
        string requestFingerprint)
    {
        if (!outbox.TryCapture(stepOperationId, out ProductionPhysicalCustodyDrainSaveData state))
            return Conflict("production-physical-custody-drain-missing");
        if (!string.Equals(
                state.requestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict("production-physical-custody-drain-request-conflict");
        }
        if (state.phase is ProductionPhysicalCustodyDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or ProductionPhysicalCustodyDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
        {
            return Current(state, ProductionPhysicalCustodyDrainStatus.Replay);
        }

        return state.phase switch
        {
            ProductionPhysicalCustodyDrainPhase.Prepared =>
                BeginActorRelease(state),
            ProductionPhysicalCustodyDrainPhase.ReleasingActors =>
                ReleaseNextActor(state),
            ProductionPhysicalCustodyDrainPhase.ReleasingIntents =>
                ReleaseNextIntent(state),
            ProductionPhysicalCustodyDrainPhase.ReleasingDestination =>
                ReleaseDestination(state),
            _ => Conflict("production-physical-custody-drain-phase-invalid")
        };
    }

    public ProductionPhysicalCustodyDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint) => outbox.TryAcknowledge(
            stepOperationId,
            receiptFingerprint);

    public ProductionPhysicalCustodyDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint) => outbox.TryGarbageCollect(
            stepOperationId,
            receiptFingerprint);

    public bool TryCapture(
        string stepOperationId,
        out ProductionPhysicalCustodyDrainSaveData record) => outbox.TryCapture(
            stepOperationId,
            out record);

    private ProductionPhysicalCustodyDrainResult BeginActorRelease(
        ProductionPhysicalCustodyDrainSaveData state)
    {
        ProductionPhysicalCustodyDrainResult routeGate = CheckRouteGate(state);
        if (routeGate.Status is ProductionPhysicalCustodyDrainStatus.Deferred
            or ProductionPhysicalCustodyDrainStatus.Conflict)
        {
            return routeGate;
        }
        if (!TryPreflightRemainingActors(
                state,
                out ProductionPhysicalCustodyDrainStatus preflightStatus,
                out string failureReason))
        {
            return preflightStatus == ProductionPhysicalCustodyDrainStatus.Conflict
                ? Conflict(failureReason)
                : Deferred(failureReason);
        }
        return outbox.TryBeginDraining(
            state.stepOperationId,
            state.requestFingerprint);
    }

    private ProductionPhysicalCustodyDrainResult ReleaseNextActor(
        ProductionPhysicalCustodyDrainSaveData state)
    {
        ProductionPhysicalCustodyDrainResult routeGate = CheckRouteGate(state);
        if (routeGate.Status is ProductionPhysicalCustodyDrainStatus.Deferred
            or ProductionPhysicalCustodyDrainStatus.Conflict)
        {
            return routeGate;
        }

        string[] remaining = state.sourceActorIds
            .Except(state.completedActorIds, StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (remaining.Length == 0)
            return outbox.TryBeginReleasingIntents(state.stepOperationId);
        if (!TryPreflightRemainingActors(
                state,
                out ProductionPhysicalCustodyDrainStatus preflightStatus,
                out string failureReason))
        {
            return preflightStatus == ProductionPhysicalCustodyDrainStatus.Conflict
                ? Conflict(failureReason)
                : Deferred(failureReason);
        }

        string actorId = remaining[0];
        string[] allowedOperations = OperationsForActor(state, actorId);
        if (!TryFindActor(actorId, out CharacterActor actor, out string actorFailure))
        {
            if (!actorFailure.StartsWith(
                    "production-physical-custody-actor-missing:",
                    StringComparison.Ordinal)
                || HasAnyLiveOperationAuthority(allowedOperations))
            {
                return Conflict(actorFailure);
            }
            using (mutationGate.EnterCaptureBarrier())
            {
                return outbox.TryRecordActorCompleted(
                    state.stepOperationId,
                    actorId);
            }
        }
        if (actor != null)
        {
            AbilityHaul haul = actor.GetComponent<AbilityHaul>();
            if (haul == null)
                return Conflict("production-physical-custody-actor-haul-missing:" + actorId);
            bool hasRelevantPlan = haul.CaptureActiveHaulOperationIds()
                .Any(value => allowedOperations.Contains(
                    value,
                    StringComparer.Ordinal));
            bool hasRelevantCargo = actor.CarryInventory?.Items.Any(item => item != null
                && item.quantity > 0
                && allowedOperations.Contains(
                    item.ownerOperationId ?? string.Empty,
                    StringComparer.Ordinal)) == true;
            if (hasRelevantCargo && !hasRelevantPlan)
            {
                return Deferred(
                    "production-physical-custody-carried-plan-unbound:" + actorId);
            }
            if (hasRelevantPlan)
            {
                using IDisposable barrier = mutationGate.EnterCaptureBarrier();
                if (!haul.TryStopHaulingIfActiveOperationsSubsetOf(
                        allowedOperations,
                        InterruptionReason,
                        HaulInterruptionDisposition
                            .ReleaseUnpickedAndDropCarriedAtActor,
                        out string stopFailure))
                {
                    return Deferred(
                        "production-physical-custody-actor-release-deferred:"
                        + actorId + ":" + stopFailure);
                }
                return outbox.TryRecordActorCompleted(
                    state.stepOperationId,
                    actorId);
            }
        }

        using (mutationGate.EnterCaptureBarrier())
        {
            return outbox.TryRecordActorCompleted(
                state.stepOperationId,
                actorId);
        }
    }

    private ProductionPhysicalCustodyDrainResult ReleaseNextIntent(
        ProductionPhysicalCustodyDrainSaveData state)
    {
        string[] remaining = state.sourceHaulIntentOperationIds
            .Except(state.releasedHaulIntentOperationIds, StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (remaining.Length == 0)
            return outbox.TryBeginReleasingDestination(state.stepOperationId);

        string operationId = remaining[0];
        if (AnyActorOwnsOperation(operationId)
            || AnyActorCarriesOperation(operationId))
        {
            return Deferred(
                "production-physical-custody-intent-still-actor-owned:"
                + operationId);
        }

        using (mutationGate.EnterCaptureBarrier())
        {
            reservations.ReleaseByOwner(
                operationId,
                ItemReservationReleaseReason.Cancelled);
            if (worldItems.TryCaptureHaulDeliveryIntent(operationId, out _)
                && !worldItems.ReleaseHaulDeliveryIntent(operationId))
            {
                return Conflict(
                    "production-physical-custody-intent-release-failed:"
                    + operationId);
            }
            if ((reservations.TryGetLeasesByOwner(
                     operationId,
                     out IReadOnlyList<ItemQuantityLease> leases)
                    && leases.Count > 0)
                || worldItems.TryCaptureHaulDeliveryIntent(operationId, out _))
            {
                return Conflict(
                    "production-physical-custody-intent-release-incomplete:"
                    + operationId);
            }
            return outbox.TryRecordHaulIntentReleased(
                state.stepOperationId,
                operationId);
        }
    }

    private ProductionPhysicalCustodyDrainResult ReleaseDestination(
        ProductionPhysicalCustodyDrainSaveData state)
    {
        ProductionPhysicalCustodyDrainResult routeGate = CheckRouteGate(state);
        if (routeGate.Status is ProductionPhysicalCustodyDrainStatus.Deferred
            or ProductionPhysicalCustodyDrainStatus.Conflict)
        {
            return routeGate;
        }
        foreach (string operationId in state.sourceHaulIntentOperationIds)
        {
            if (AnyActorOwnsOperation(operationId)
                || AnyActorCarriesOperation(operationId)
                || worldItems.TryCaptureHaulDeliveryIntent(operationId, out _)
                || reservations.TryGetLeasesByOwner(
                    operationId,
                    out IReadOnlyList<ItemQuantityLease> leases)
                    && leases.Count > 0)
            {
                return Deferred(
                    "production-physical-custody-destination-owner-active:"
                    + operationId);
            }
        }

        HashSet<string> frozen = state.sourceStackIds.ToHashSet(
            StringComparer.Ordinal);
        WorldItemStackRecord[] destinationRecords = repository.Records
            .Where(record => record != null
                && record.quantity > 0
                && string.Equals(
                    record.destinationId,
                    state.sourceDestinationId,
                    StringComparison.Ordinal))
            .OrderBy(record => record.stackId, StringComparer.Ordinal)
            .ToArray();
        string foreign = destinationRecords
            .Select(record => record.stackId)
            .FirstOrDefault(stackId => !frozen.Contains(stackId));
        if (!string.IsNullOrEmpty(foreign))
        {
            return Conflict(
                "production-physical-custody-late-destination-stack:" + foreign);
        }
        if (!TryVerifyFrozenVector(state, requireReleased: false, out string failure))
            return Conflict(failure);

        int expectedDestinationQuantity = destinationRecords.Sum(
            record => record.quantity);
        using (mutationGate.EnterCaptureBarrier())
        {
            int released = expectedDestinationQuantity == 0
                ? 0
                : transfers.ReleaseDestination(
                    state.sourceDestinationId,
                    new Vector2Int(state.ownerGridX, state.ownerGridY));
            if (released != expectedDestinationQuantity)
            {
                return Conflict(
                    "production-physical-custody-destination-release-quantity-conflict");
            }
            if (!TryVerifyFrozenVector(state, requireReleased: true, out failure))
                return Conflict(failure);

            string resultFingerprint = CreateResultFingerprint(state);
            return outbox.TryCommitEffect(
                state.stepOperationId,
                state.sourceStackIds,
                state.inputQuantity,
                state.inputMassGrams,
                resultFingerprint);
        }
    }

    private bool TryPreflightRemainingActors(
        ProductionPhysicalCustodyDrainSaveData state,
        out ProductionPhysicalCustodyDrainStatus failureStatus,
        out string failureReason)
    {
        failureStatus = ProductionPhysicalCustodyDrainStatus.Deferred;
        failureReason = string.Empty;
        CharacterActor[] lifetimeActors = (characterLifetime.AllCharacters
                ?? Array.Empty<CharacterActor>())
            .Where(value => value != null)
            .ToArray();
        foreach (string sourceActorId in state.sourceActorIds)
        {
            int actorCount = lifetimeActors.Count(value => string.Equals(
                value.BuildingCharacterId.Value,
                sourceActorId,
                StringComparison.Ordinal));
            if (actorCount > 1)
            {
                failureStatus = ProductionPhysicalCustodyDrainStatus.Conflict;
                failureReason =
                    "production-physical-custody-actor-duplicate:"
                    + sourceActorId;
                return false;
            }
            if (actorCount == 0
                && HasAnyLiveOperationAuthority(
                    OperationsForActor(state, sourceActorId)))
            {
                failureStatus = ProductionPhysicalCustodyDrainStatus.Conflict;
                failureReason =
                    "production-physical-custody-actor-missing-with-authority:"
                    + sourceActorId;
                return false;
            }
        }
        foreach (string operationId in state.sourceHaulIntentOperationIds)
        {
            string[] expectedActors = state.sourceActorIds
                .Where(actorId => HaulDeliveryOperationIdentity.TryParse(
                    operationId,
                    actorId,
                    out _))
                .ToArray();
            if (expectedActors.Length != 1)
            {
                failureStatus = ProductionPhysicalCustodyDrainStatus.Conflict;
                failureReason =
                    "production-physical-custody-source-operation-owner-invalid:"
                    + operationId;
                return false;
            }
            string expectedActorId = expectedActors[0];
            bool hasIntent = worldItems.TryCaptureHaulDeliveryIntent(
                    operationId,
                    out HaulDeliveryIntentSaveData sourceIntent)
                && sourceIntent != null;
            if (hasIntent
                && (!IsToken(sourceIntent.ownerCharacterId)
                    || !string.Equals(
                        sourceIntent.ownerCharacterId,
                        expectedActorId,
                        StringComparison.Ordinal)))
            {
                failureStatus = ProductionPhysicalCustodyDrainStatus.Conflict;
                failureReason =
                    "production-physical-custody-source-intent-owner-conflict:"
                    + operationId;
                return false;
            }
            bool anyLivePlanOrCargo = false;
            foreach (CharacterActor candidate in lifetimeActors)
            {
                string candidateId = candidate.BuildingCharacterId.Value;
                AbilityHaul candidateHaul = candidate.GetComponent<AbilityHaul>();
                bool ownsPlan = candidateHaul?.OwnsHaulOperation(operationId) == true;
                bool ownsCargo = candidate.CarryInventory?.Items.Any(item =>
                    item != null
                    && item.quantity > 0
                    && string.Equals(
                        item.ownerOperationId,
                        operationId,
                        StringComparison.Ordinal)) == true;
                anyLivePlanOrCargo |= ownsPlan || ownsCargo;
                if ((ownsPlan || ownsCargo)
                    && !string.Equals(
                        candidateId,
                        expectedActorId,
                        StringComparison.Ordinal))
                {
                    failureStatus = ProductionPhysicalCustodyDrainStatus.Conflict;
                    failureReason =
                        "production-physical-custody-cross-actor-ownership:"
                        + operationId + ":" + candidateId;
                    return false;
                }
            }
            bool hasLeases = reservations.TryGetLeasesByOwner(
                    operationId,
                    out IReadOnlyList<ItemQuantityLease> operationLeases)
                && operationLeases != null
                && operationLeases.Count > 0;
            if (!hasIntent && (anyLivePlanOrCargo || hasLeases))
            {
                failureStatus = ProductionPhysicalCustodyDrainStatus.Conflict;
                failureReason =
                    "production-physical-custody-source-intent-orphaned-authority:"
                    + operationId;
                return false;
            }
        }
        foreach (string actorId in state.sourceActorIds
                     .Except(state.completedActorIds, StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!TryFindActor(actorId, out CharacterActor actor, out string actorFailure))
            {
                if (actorFailure.StartsWith(
                        "production-physical-custody-actor-missing:",
                        StringComparison.Ordinal)
                    && !HasAnyLiveOperationAuthority(
                        OperationsForActor(state, actorId)))
                {
                    continue;
                }
                failureStatus = ProductionPhysicalCustodyDrainStatus.Conflict;
                failureReason = actorFailure;
                return false;
            }
            string[] allowed = OperationsForActor(state, actorId);
            if (allowed.Length == 0)
            {
                failureReason =
                    "production-physical-custody-actor-operation-missing:"
                    + actorId;
                failureStatus = ProductionPhysicalCustodyDrainStatus.Conflict;
                return false;
            }
            AbilityHaul haul = actor.GetComponent<AbilityHaul>();
            if (haul == null)
            {
                failureReason =
                    "production-physical-custody-actor-haul-missing:" + actorId;
                failureStatus = ProductionPhysicalCustodyDrainStatus.Conflict;
                return false;
            }
            string foreignPlan = haul.CaptureActiveHaulOperationIds()
                .FirstOrDefault(operationId => !allowed.Contains(
                    operationId,
                    StringComparer.Ordinal));
            if (!string.IsNullOrEmpty(foreignPlan))
            {
                failureReason = "mixed-destination-active-plan:" + foreignPlan;
                return false;
            }
            CharacterCarriedItemSaveData[] carried = (actor.CarryInventory?.Items
                    ?? Array.Empty<CharacterCarriedItemSaveData>())
                .Where(item => item != null && item.quantity > 0)
                .ToArray();
            CharacterCarriedItemSaveData malformedCargo = carried.FirstOrDefault(
                item => !IsToken(item.ownerOperationId));
            if (malformedCargo != null)
            {
                failureStatus = ProductionPhysicalCustodyDrainStatus.Conflict;
                failureReason = "carried-cargo-owner-invalid:" + actorId;
                return false;
            }
            string foreignCargo = carried
                .Select(item => item.ownerOperationId)
                .FirstOrDefault(operationId => !allowed.Contains(
                    operationId,
                    StringComparer.Ordinal));
            if (!string.IsNullOrEmpty(foreignCargo))
            {
                failureReason = "mixed-destination-carried-cargo:" + foreignCargo;
                return false;
            }
            foreach (string operationId in allowed)
            {
                if (!worldItems.TryCaptureHaulDeliveryIntent(
                        operationId,
                        out HaulDeliveryIntentSaveData intent)
                    || !intent.HasCommittedPickup)
                {
                    continue;
                }
                bool ownsOperation = haul.OwnsHaulOperation(operationId);
                bool carryMatches = CommittedCarryMatches(
                    actor,
                    intent,
                    out string carryFailure);
                if (!ownsOperation || !carryMatches)
                {
                    failureStatus = ProductionPhysicalCustodyDrainStatus.Conflict;
                    failureReason =
                        "production-physical-custody-carried-preflight-failed:"
                        + operationId + ":"
                        + (ownsOperation ? carryFailure : "active-plan-missing");
                    return false;
                }
            }
        }
        return true;
    }

    private ProductionPhysicalCustodyDrainResult CheckRouteGate(
        ProductionPhysicalCustodyDrainSaveData state)
    {
        string[] pending = exactRoutes.CapturePendingRoutes()
            .Where(value => value?.Receipt != null
                && string.Equals(
                    value.Receipt.SourceDestinationId,
                    state.sourceDestinationId,
                    StringComparison.Ordinal))
            .Select(value => value.Receipt.RouteOperationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] custody = repository.Records
            .Where(record => record != null
                && record.quantity > 0
                && FacilityOutputExactRouteCustodyCodec.TryRead(
                    record.components,
                    out FacilityOutputExactRouteCustodyMetadata metadata)
                && string.Equals(
                    metadata.OriginDestinationId,
                    state.sourceDestinationId,
                    StringComparison.Ordinal))
            .Select(record =>
            {
                FacilityOutputExactRouteCustodyCodec.TryRead(
                    record.components,
                    out FacilityOutputExactRouteCustodyMetadata metadata);
                return metadata.RouteOperationId;
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (pending.Length == 0 && custody.Length == 0)
            return Current(state, ProductionPhysicalCustodyDrainStatus.Applied);
        if (pending.SequenceEqual(custody, StringComparer.Ordinal))
        {
            return Deferred(
                "production-physical-custody-route-predecessor-active:"
                + string.Join(",", pending));
        }
        return Conflict(
            "production-physical-custody-route-authority-inconsistent");
    }

    private bool TryVerifyFrozenVector(
        ProductionPhysicalCustodyDrainSaveData state,
        bool requireReleased,
        out string failureReason)
    {
        failureReason = string.Empty;
        int quantity = 0;
        long mass = 0L;
        foreach (string stackId in state.sourceStackIds)
        {
            if (!repository.RecordsById.TryGetValue(
                    stackId,
                    out WorldItemStackRecord record)
                || record == null
                || record.quantity <= 0)
            {
                failureReason =
                    "production-physical-custody-frozen-stack-missing:" + stackId;
                return false;
            }
            if (requireReleased
                && (record.state is WorldItemStackState.Carried
                        or WorldItemStackState.InTransit
                    || string.Equals(
                        record.destinationId,
                        state.sourceDestinationId,
                        StringComparison.Ordinal)
                    || HasOriginCustody(record, state.sourceDestinationId)))
            {
                failureReason =
                    "production-physical-custody-stack-not-released:" + stackId;
                return false;
            }
            try
            {
                quantity = checked(quantity + record.quantity);
                mass = checked(mass + GetMass(record));
            }
            catch (Exception exception) when (exception is OverflowException
                or ArgumentException or InvalidOperationException)
            {
                failureReason =
                    "production-physical-custody-frozen-stack-invalid:"
                    + stackId + ":" + exception.GetType().Name;
                return false;
            }
        }
        if (quantity != state.inputQuantity || mass != state.inputMassGrams)
        {
            failureReason =
                "production-physical-custody-frozen-vector-total-conflict";
            return false;
        }
        return true;
    }

    private string CreateResultFingerprint(
        ProductionPhysicalCustodyDrainSaveData state)
    {
        StringBuilder canonical = new StringBuilder(256)
            .Append("production-physical-custody-drain-result@1|")
            .Append(state.stepOperationId).Append('|')
            .Append(state.requestFingerprint).Append('|');
        foreach (string stackId in state.sourceStackIds)
        {
            WorldItemStackRecord record = repository.RecordsById[stackId];
            string componentSignature = ItemReservationSignature.Create(
                record.itemId,
                record.components);
            canonical.Append(stackId.Length).Append(':').Append(stackId).Append('|')
                .Append(record.itemId.Length).Append(':').Append(record.itemId).Append('|')
                .Append(record.quantity.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(GetMass(record).ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(((int)record.state).ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(record.position.x.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(record.position.y.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(record.destinationId.Length).Append(':')
                .Append(record.destinationId).Append('|')
                .Append(componentSignature.Length).Append(':')
                .Append(componentSignature).Append(';');
        }
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            Encoding.UTF8.GetBytes(canonical.ToString()));
        StringBuilder result = new StringBuilder(digest.Length * 2);
        foreach (byte value in digest)
            result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private long GetMass(WorldItemStackRecord record)
    {
        ItemDefinitionId itemId = (ItemDefinitionId)record.itemId;
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            worldItems.MassQuery,
            itemId,
            record.itemInstanceId,
            record.components);
        return worldItems.MassQuery.GetQuantityMass(
            itemId,
            subject,
            record.quantity).Value;
    }

    private bool TryFindActor(
        string actorId,
        out CharacterActor actor,
        out string failureReason)
    {
        actor = null;
        failureReason = string.Empty;
        CharacterActor[] matches = (characterLifetime.AllCharacters
                ?? Array.Empty<CharacterActor>())
            .Where(value => value != null
                && string.Equals(
                    value.BuildingCharacterId.Value,
                    actorId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            failureReason = matches.Length == 0
                ? "production-physical-custody-actor-missing:" + actorId
                : "production-physical-custody-actor-duplicate:" + actorId;
            return false;
        }
        actor = matches[0];
        return true;
    }

    private static string[] OperationsForActor(
        ProductionPhysicalCustodyDrainSaveData state,
        string actorId) => state.sourceHaulIntentOperationIds
        .Where(operationId => HaulDeliveryOperationIdentity.TryParse(
            operationId,
            actorId,
            out _))
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private bool AnyActorOwnsOperation(string operationId) =>
        (characterLifetime.AllCharacters ?? Array.Empty<CharacterActor>())
        .Where(actor => actor != null)
        .Select(actor => actor.GetComponent<AbilityHaul>())
        .Any(haul => haul != null && haul.OwnsHaulOperation(operationId));

    private bool AnyActorCarriesOperation(string operationId) =>
        (characterLifetime.AllCharacters ?? Array.Empty<CharacterActor>())
        .Where(actor => actor?.CarryInventory != null)
        .SelectMany(actor => actor.CarryInventory.Items)
        .Any(item => item != null
            && item.quantity > 0
            && string.Equals(
                item.ownerOperationId,
                operationId,
                StringComparison.Ordinal));

    private bool HasAnyLiveOperationAuthority(
        IEnumerable<string> operationIds)
    {
        foreach (string operationId in operationIds ?? Array.Empty<string>())
        {
            if (worldItems.TryCaptureHaulDeliveryIntent(operationId, out _)
                || (reservations.TryGetLeasesByOwner(
                        operationId,
                        out IReadOnlyList<ItemQuantityLease> leases)
                    && leases != null
                    && leases.Count > 0)
                || AnyActorOwnsOperation(operationId)
                || AnyActorCarriesOperation(operationId))
            {
                return true;
            }
        }
        return false;
    }

    private bool CommittedCarryMatches(
        CharacterActor actor,
        HaulDeliveryIntentSaveData intent,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor?.CarryInventory == null
            || intent?.commitments == null
            || !intent.HasCommittedPickup)
        {
            failureReason = "authority-missing";
            return false;
        }

        HaulDeliveryItemCommitmentSaveData[] commitments = intent.commitments
            .Where(value => value != null && value.quantity > 0)
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ToArray();
        CharacterCarriedItemSaveData[] carried = actor.CarryInventory.Items
            .Where(value => value != null
                && value.quantity > 0
                && string.Equals(
                    value.ownerOperationId,
                    intent.operationId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ToArray();
        if (commitments.Length != intent.commitments.Count
            || carried.Length != commitments.Length
            || commitments.Select(value => value.carriedStackId)
                .Distinct(StringComparer.Ordinal).Count() != commitments.Length)
        {
            failureReason = "set-cardinality-mismatch";
            return false;
        }
        if (!reservations.TryGetLeasesByOwner(
                intent.operationId,
                out IReadOnlyList<ItemQuantityLease> operationLeases)
            || operationLeases == null)
        {
            failureReason = "operation-lease-authority-missing";
            return false;
        }
        ItemQuantityLease[] haulLeases = operationLeases
            .Where(value => value != null
                && value.purpose == ItemReservationPurpose.Hauling)
            .OrderBy(value => value.leaseId, StringComparer.Ordinal)
            .ToArray();
        if (haulLeases.Length != commitments.Length
            || operationLeases.Count != haulLeases.Length)
        {
            failureReason = "operation-lease-set-mismatch";
            return false;
        }

        for (int index = 0; index < commitments.Length; index++)
        {
            HaulDeliveryItemCommitmentSaveData commitment = commitments[index];
            CharacterCarriedItemSaveData item = carried[index];
            if (!IsToken(commitment.carriedStackId)
                || !IsToken(commitment.sourceStackId)
                || !IsToken(commitment.itemId)
                || !IsToken(commitment.expectedStackSignature)
                || !string.Equals(
                    item.carriedStackId,
                    commitment.carriedStackId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    item.sourceStackId,
                    commitment.sourceStackId,
                    StringComparison.Ordinal)
                || !string.Equals(item.itemId, commitment.itemId,
                    StringComparison.Ordinal)
                || item.quantity != commitment.quantity
                || !string.Equals(
                    ItemReservationSignature.Create(item.itemId, item.components),
                    commitment.expectedStackSignature,
                    StringComparison.Ordinal)
                || haulLeases.Count(lease => lease.remainingQuantity
                        == commitment.quantity
                    && string.Equals(
                        lease.ownerOperationId,
                        intent.operationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        lease.ownerCharacterId,
                        intent.ownerCharacterId,
                        StringComparison.Ordinal)
                    && lease.slices != null
                    && lease.slices.Count == 1
                    && lease.slices[0] != null
                    && lease.slices[0].quantity == commitment.quantity
                    && string.Equals(
                        lease.slices[0].stackId,
                        commitment.carriedStackId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        lease.slices[0].expectedStackSignature,
                        commitment.expectedStackSignature,
                        StringComparison.Ordinal)) != 1
                || !repository.RecordsById.TryGetValue(
                    commitment.carriedStackId,
                    out WorldItemStackRecord record)
                || record == null
                || record.state != WorldItemStackState.Carried
                || record.quantity != commitment.quantity
                || !string.Equals(record.itemId, commitment.itemId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    record.itemInstanceId,
                    item.itemInstanceId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    record.destinationId,
                    intent.ownerCharacterId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    ItemReservationSignature.Create(record.itemId, record.components),
                    commitment.expectedStackSignature,
                    StringComparison.Ordinal))
            {
                failureReason = "commitment-join-mismatch:"
                    + (commitment.carriedStackId ?? string.Empty);
                return false;
            }
        }
        return true;
    }

    private static bool TryAddIntent(
        HaulDeliveryIntentSaveData intent,
        IDictionary<string, HaulDeliveryIntentSaveData> target,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (intent == null
            || !IsToken(intent.operationId)
            || !IsToken(intent.ownerCharacterId))
        {
            failureReason =
                "production-physical-custody-haul-intent-invalid";
            return false;
        }
        if (target.TryGetValue(
                intent.operationId,
                out HaulDeliveryIntentSaveData existing))
        {
            if (!string.Equals(
                    existing.ownerCharacterId,
                    intent.ownerCharacterId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    existing.destinationId,
                    intent.destinationId,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "production-physical-custody-haul-intent-conflict:"
                    + intent.operationId;
                return false;
            }
            return true;
        }
        target.Add(intent.operationId, intent);
        return true;
    }

    private static bool IsSourceRecord(
        WorldItemStackRecord record,
        string sourceDestinationId) => record != null
        && record.quantity > 0
        && (record.state == WorldItemStackState.FacilityOutputBuffer
            && string.Equals(
                record.destinationId,
                sourceDestinationId,
                StringComparison.Ordinal)
            || HasOriginCustody(record, sourceDestinationId));

    private static bool HasOriginCustody(
        WorldItemStackRecord record,
        string sourceDestinationId) =>
        FacilityOutputExactRouteCustodyCodec.TryRead(
            record?.components,
            out FacilityOutputExactRouteCustodyMetadata custody)
        && string.Equals(
            custody.OriginDestinationId,
            sourceDestinationId,
            StringComparison.Ordinal);

    private static bool IsToken(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsDigest(string value) => value?.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private static ProductionPhysicalCustodyDrainResult Current(
        ProductionPhysicalCustodyDrainSaveData state,
        ProductionPhysicalCustodyDrainStatus status) => new(
            status,
            state.commitId,
            state.receiptFingerprint,
            string.Empty);

    private static ProductionPhysicalCustodyDrainResult Deferred(string reason) =>
        new(
            ProductionPhysicalCustodyDrainStatus.Deferred,
            string.Empty,
            string.Empty,
            reason);

    private static ProductionPhysicalCustodyDrainResult Conflict(string reason) =>
        new(
            ProductionPhysicalCustodyDrainStatus.Conflict,
            string.Empty,
            string.Empty,
            reason);
}
