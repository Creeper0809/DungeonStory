using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using UnityEngine;

internal enum PreparedOutputPickupBoundaryFailureCode
{
    None = 0,
    CustodyMalformed = 1,
    IntentMissing = 2,
    IntentProjectionStale = 3,
    QuantityLeaseStale = 4,
    SourceLotStale = 5,
    AdmissionMissing = 6,
    AdmissionNotReserved = 7,
    AdmissionProjectionStale = 8,
    DestinationAuthorityStale = 9
}

internal readonly struct PreparedOutputPickupBoundaryResult
{
    internal PreparedOutputPickupBoundaryResult(
        PreparedOutputPickupBoundaryFailureCode code,
        string detail = "")
    {
        Code = code;
        Detail = detail ?? string.Empty;
    }

    internal PreparedOutputPickupBoundaryFailureCode Code { get; }
    internal string Detail { get; }
    internal bool IsFailure => Code != PreparedOutputPickupBoundaryFailureCode.None;

    public override string ToString() => IsFailure
        ? $"items.haul.prepared_output_pickup_boundary:{Code}:{Detail}"
        : string.Empty;
}

/// <summary>
/// Routes repository-owned physical stacks into and out of warehouses. Warehouse
/// inventories provide capacity/filter policy only and never author quantities.
/// </summary>
public sealed class WorldItemWarehouseService
{
    private sealed class DeliveryRetargetSlice
    {
        internal WorldItemStackRecord Source;
        internal int Quantity;
        internal string SourceStorageDestinationId = string.Empty;
    }

    private sealed class DeliveryRetargetRecordSnapshot
    {
        internal WorldItemStackRecord Record;
        internal int Quantity;
        internal WorldItemStackState State;
        internal string DestinationId;
        internal string SourceStorageDestinationId;
        internal bool HasDestinationPosition;
        internal Vector2Int DestinationPosition;
        internal string ReservedByPersistentId;
        internal int ReservedQuantity;
        internal string AggregationCohortId;
        internal WorldItemDropDisposition DropDisposition;
        internal string RecoveryOwnerOperationId;
        internal string RecoverySourceStackId;
        internal string RecoveryCarrierPersistentId;
        internal WorldItemCarryInterruptionKind RecoveryInterruptionKind;
        internal double DroppedAtGameTime;
        internal double RecoveryDeadlineGameTime;
    }

    private sealed class WarehouseItemMutationUndoJournal
    {
        internal readonly Dictionary<string, int> QuantityByStackId =
            new(StringComparer.Ordinal);
    }

    private readonly IDungeonItemCatalogProvider catalog;
    private readonly WorldItemRepository repository;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorldItemSpawner spawner;
    private readonly IItemMarkerPresenter markers;
    private readonly IGridSystemProvider gridProvider;
    private readonly ICharacterIdRegistry characterIds;
    private readonly IItemReservationService reservations;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly IWarehouseMassAdmissionService massAdmission;
    private readonly IFacilityBufferMassAdmissionService facilityBufferMassAdmission;
    private long nextMassIngressSequence = 1L;
    private long nextFacilityBufferAdmissionSequence = 1L;

    public WorldItemWarehouseService(
        IDungeonItemCatalogProvider catalog,
        WorldItemRepository repository,
        ICharacterAiWorldRegistry worldRegistry,
        IWorldItemSpawner spawner,
        IItemMarkerPresenter markers,
        IGridSystemProvider gridProvider,
        ICharacterIdRegistry characterIds,
        IItemReservationService reservations,
        IItemQuantityReservationService quantityReservations = null,
        IWarehouseMassAdmissionService massAdmission = null,
        IFacilityBufferMassAdmissionService facilityBufferMassAdmission = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        this.markers = markers ?? throw new ArgumentNullException(nameof(markers));
        this.gridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        this.characterIds = characterIds
            ?? throw new ArgumentNullException(nameof(characterIds));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.quantityReservations = quantityReservations;
        this.massAdmission = massAdmission;
        this.facilityBufferMassAdmission = facilityBufferMassAdmission;
    }

    internal bool TryCapturePreparedOutputAuthority(
        PreparedOutputExactDestinationTargetKind kind,
        string destinationId,
        Vector2Int position,
        out PreparedOutputExactDestinationAuthoritySnapshot snapshot,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason)
    {
        snapshot = default;
        failureCode = PreparedOutputExactDestinationAdmissionFailureCode.None;
        failureReason = string.Empty;
        string destination = destinationId ?? string.Empty;
        if (!IsCanonicalRequired(destination)
            || !Enum.IsDefined(typeof(PreparedOutputExactDestinationTargetKind), kind))
        {
            return FailPreparedOutputAdmission(
                PreparedOutputExactDestinationAdmissionFailureCode.InvalidRequest,
                "prepared-output destination authority request invalid",
                out failureCode,
                out failureReason);
        }

        if (kind == PreparedOutputExactDestinationTargetKind.FacilityBuffer)
        {
            if (facilityBufferMassAdmission == null
                || !facilityBufferMassAdmission.TryGetCapacity(
                    destination,
                    position,
                    out FacilityBufferMassCapacitySnapshot capacity)
                || capacity.Profile == null
                || capacity.Profile.MaxMassGrams <= 0L
                || !facilityBufferMassAdmission.TryGetCapacityAuthorityFingerprint(
                    destination,
                    position,
                    out string fingerprint))
            {
                return FailPreparedOutputAdmission(
                    PreparedOutputExactDestinationAdmissionFailureCode.AuthorityMissing,
                    "prepared-output facility-buffer capacity authority unavailable",
                    out failureCode,
                    out failureReason);
            }
            snapshot = new PreparedOutputExactDestinationAuthoritySnapshot(
                kind,
                destination,
                position,
                fingerprint,
                capacity.Profile.CapacityRevision,
                capacity.MassAuthorityRevision,
                capacity.Profile.MaxMassGrams,
                capacity.ReservedMassGrams);
            return true;
        }

        string warehouseIdValue = ParseWarehouseIdOrEmpty(destination);
        IWarehouseFacility warehouse = GetWarehouses().SingleOrDefault(value =>
            string.Equals(
                value.PersistentInstanceId.Value,
                warehouseIdValue,
                StringComparison.Ordinal));
        Vector2Int warehousePosition = warehouse is BuildableObject building
            ? building.centerPos
            : default;
        if (massAdmission == null
            || warehouse?.Inventory?.HasMassCapacityAuthority != true
            || warehousePosition != position
            || warehouse.Inventory.MaxMassGrams <= 0L)
        {
            return FailPreparedOutputAdmission(
                PreparedOutputExactDestinationAdmissionFailureCode.AuthorityMissing,
                "prepared-output warehouse capacity authority unavailable",
                out failureCode,
                out failureReason);
        }
        long capacityRevision = massAdmission.GetWarehouseCapacityRevision(
            warehouse.PersistentInstanceId);
        long massRevision = massAdmission.CatalogRevision;
        long reserved = massAdmission.GetReservedInboundMassGrams(
            warehouse.PersistentInstanceId);
        string warehouseFingerprint = HashPreparedOutputAuthority(
            "warehouse-capacity-v1",
            destination,
            position,
            warehouse.Inventory.MaxMassGrams,
            capacityRevision,
            massRevision,
            warehouse.Inventory.AcceptedCategory.ToString());
        snapshot = new PreparedOutputExactDestinationAuthoritySnapshot(
            kind,
            destination,
            position,
            warehouseFingerprint,
            capacityRevision,
            massRevision,
            warehouse.Inventory.MaxMassGrams,
            reserved);
        return true;
    }

    /// <summary>
    /// Revalidates every destination and source authority needed by a prepared
    /// output pickup. The caller invokes this in the same synchronous method,
    /// immediately before its first source-side mutation. Ordinary haul lots
    /// never enter this boundary.
    /// </summary>
    internal PreparedOutputPickupBoundaryResult
        ValidatePreparedOutputPickupBoundary(
            WorldItemReservedStackQuantity reservation,
            ItemQuantityLease lease,
            WorldItemStackRecord source,
            int pickupQuantity,
            long exactMassGrams)
    {
        if (source == null
            || !FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                source.components))
        {
            return default;
        }
        if (!FacilityOutputExactRouteCustodyCodec.TryRead(
                source.components,
                out FacilityOutputExactRouteCustodyMetadata custody))
        {
            return FailPreparedOutputPickup(
                PreparedOutputPickupBoundaryFailureCode.CustodyMalformed,
                source?.stackId);
        }
        if (!repository.HaulDeliveryIntents.TryCapture(
                reservation.OwnerOperationId,
                out HaulDeliveryIntentSaveData intent))
        {
            return FailPreparedOutputPickup(
                PreparedOutputPickupBoundaryFailureCode.IntentMissing,
                reservation.OwnerOperationId);
        }

        PreparedOutputExactDestinationAuthoritySnapshot authority = default;
        PreparedOutputExactDestinationAdmissionFailureCode authorityFailure =
            PreparedOutputExactDestinationAdmissionFailureCode.None;
        string authorityReason = string.Empty;
        bool hasAuthority = TryCapturePreparedOutputAuthority(
            PreparedOutputExactDestinationTargetKind.Warehouse,
            custody.CurrentTargetDestinationId,
            custody.CurrentTargetPosition,
            out authority,
            out authorityFailure,
            out authorityReason);

        WarehouseHaulAdmissionSaveData[] admissions = (intent.warehouseAdmissions
                ?? new List<WarehouseHaulAdmissionSaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.sourceStackId,
                    source.stackId,
                    StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (admissions.Length != 1)
        {
            return FailPreparedOutputPickup(
                PreparedOutputPickupBoundaryFailureCode.AdmissionMissing,
                source.stackId);
        }
        WarehouseHaulAdmissionSaveData admission = admissions[0];
        if (massAdmission == null
            || !massAdmission.TryGetStatus(
                admission.tokenId,
                out WarehouseMassAdmissionStatusSnapshot status))
        {
            return FailPreparedOutputPickup(
                PreparedOutputPickupBoundaryFailureCode.AdmissionMissing,
                admission.tokenId);
        }

        bool hasDeliveryProjection =
            TryResolvePreparedOutputWarehouseDeliveryPosition(
                custody.CurrentTargetDestinationId,
                out Vector2Int deliveryPosition);

        return ValidatePreparedOutputPickupBoundarySnapshot(
            reservation,
            lease,
            source,
            pickupQuantity,
            exactMassGrams,
            custody,
            intent,
            admission,
            status,
            hasDeliveryProjection,
            deliveryPosition,
            hasAuthority,
            authority,
            authorityFailure + ":" + authorityReason);
    }

    internal static PreparedOutputPickupBoundaryResult
        ValidatePreparedOutputPickupBoundarySnapshot(
            WorldItemReservedStackQuantity reservation,
            ItemQuantityLease lease,
            WorldItemStackRecord source,
            int pickupQuantity,
            long exactMassGrams,
            FacilityOutputExactRouteCustodyMetadata custody,
            HaulDeliveryIntentSaveData intent,
            WarehouseHaulAdmissionSaveData admission,
            WarehouseMassAdmissionStatusSnapshot status,
            bool hasCurrentDeliveryProjection,
            Vector2Int currentDeliveryPosition,
            bool hasCurrentAuthority,
            PreparedOutputExactDestinationAuthoritySnapshot currentAuthority,
            string authorityFailureDetail = "")
    {
        string currentSignature = source == null
            ? string.Empty
            : ItemReservationSignature.Create(source.itemId, source.components);
        ItemLeaseSlice[] matchingSlices = (lease?.slices
                ?? new List<ItemLeaseSlice>())
            .Where(value => value != null
                && string.Equals(
                    value.stackId,
                    source?.stackId,
                    StringComparison.Ordinal))
            .ToArray();
        if (lease == null
            || lease.purpose != ItemReservationPurpose.Hauling
            || !string.Equals(
                lease.leaseId,
                reservation.LeaseId,
                StringComparison.Ordinal)
            || !string.Equals(
                lease.ownerOperationId,
                reservation.OwnerOperationId,
                StringComparison.Ordinal)
            || lease.remainingQuantity != pickupQuantity
            || matchingSlices.Length != 1
            || matchingSlices[0].quantity != pickupQuantity
            || !string.Equals(
                matchingSlices[0].expectedStackSignature,
                currentSignature,
                StringComparison.Ordinal))
        {
            return FailPreparedOutputPickup(
                PreparedOutputPickupBoundaryFailureCode.QuantityLeaseStale,
                reservation.LeaseId);
        }
        if (source == null
            || source.state is not (WorldItemStackState.Loose
                or WorldItemStackState.Stored
                or WorldItemStackState.FacilityBuffer)
            || source.quantity != pickupQuantity
            || reservation.Quantity != pickupQuantity
            || !string.Equals(
                reservation.StackId,
                source.stackId,
                StringComparison.Ordinal)
            || !string.Equals(
                reservation.ItemId,
                source.itemId,
                StringComparison.Ordinal)
            || custody.Phase != FacilityOutputExactRouteCustodyPhase.Routable
            || custody.Quantity != pickupQuantity
            || custody.MassGrams != exactMassGrams
            || !string.Equals(
                custody.CurrentSourceStackId,
                source.stackId,
                StringComparison.Ordinal)
            || !string.Equals(custody.ItemId, source.itemId, StringComparison.Ordinal))
        {
            return FailPreparedOutputPickup(
                PreparedOutputPickupBoundaryFailureCode.SourceLotStale,
                source?.stackId);
        }
        if (intent == null
            || !string.Equals(
                intent.operationId,
                reservation.OwnerOperationId,
                StringComparison.Ordinal)
            || intent.destinationKind != WorldItemHaulDestinationKind.Warehouse
            || reservation.DestinationKind != WorldItemHaulDestinationKind.Warehouse
            || !string.Equals(
                intent.ownerCharacterId,
                lease.ownerCharacterId,
                StringComparison.Ordinal)
            || !string.Equals(
                intent.destinationId,
                custody.CurrentTargetDestinationId,
                StringComparison.Ordinal)
            || !string.Equals(
                reservation.DestinationId,
                custody.CurrentTargetDestinationId,
                StringComparison.Ordinal)
            || !hasCurrentDeliveryProjection
            || intent.deliveryGridX != currentDeliveryPosition.x
            || intent.deliveryGridY != currentDeliveryPosition.y
            || intent.dropGridX != currentDeliveryPosition.x
            || intent.dropGridY != currentDeliveryPosition.y
            || source.destinationId != custody.CurrentTargetDestinationId
            || !source.hasDestinationPosition
            || source.destinationPosition != custody.CurrentTargetPosition)
        {
            return FailPreparedOutputPickup(
                PreparedOutputPickupBoundaryFailureCode.IntentProjectionStale,
                reservation.OwnerOperationId);
        }
        if (status.Status != WarehouseMassAdmissionTokenStatus.Reserved)
        {
            return FailPreparedOutputPickup(
                PreparedOutputPickupBoundaryFailureCode.AdmissionNotReserved,
                admission?.tokenId);
        }
        WarehouseMassAdmissionToken token = status.Token;
        string expectedWarehouseId = ParseWarehouseIdOrEmpty(
            custody.CurrentTargetDestinationId);
        if (admission == null
            || expectedWarehouseId.Length == 0
            || !string.Equals(admission.tokenId, token.TokenId,
                StringComparison.Ordinal)
            || !string.Equals(admission.ownerAdmissionOperationId,
                token.OwnerOperationId, StringComparison.Ordinal)
            || !string.Equals(admission.warehouseId,
                expectedWarehouseId, StringComparison.Ordinal)
            || !string.Equals(admission.warehouseId,
                token.WarehouseId.Value, StringComparison.Ordinal)
            || !string.Equals(admission.sourceStackId,
                source.stackId, StringComparison.Ordinal)
            || !string.Equals(admission.itemId, source.itemId,
                StringComparison.Ordinal)
            || !string.Equals(admission.itemInstanceId,
                source.itemInstanceId ?? string.Empty, StringComparison.Ordinal)
            || !string.Equals(admission.lotFingerprint,
                currentSignature, StringComparison.Ordinal)
            || !string.Equals(token.LotFingerprint,
                currentSignature, StringComparison.Ordinal)
            || admission.quantity != pickupQuantity
            || token.AcceptedQuantity != pickupQuantity
            || admission.reservedMassGrams != exactMassGrams
            || token.ReservedMassGrams != exactMassGrams
            || admission.catalogRevision != token.CatalogRevision
            || admission.sourceRevision != token.SourceRevision)
        {
            return FailPreparedOutputPickup(
                PreparedOutputPickupBoundaryFailureCode.AdmissionProjectionStale,
                admission?.tokenId);
        }
        if (!hasCurrentAuthority
            || currentAuthority.Kind !=
                PreparedOutputExactDestinationTargetKind.Warehouse
            || currentAuthority.Position != custody.CurrentTargetPosition
            || !string.Equals(currentAuthority.DestinationId,
                custody.CurrentTargetDestinationId, StringComparison.Ordinal)
            // Reserving this exact haul admission legitimately advances the
            // warehouse revision after the delivery overlay was published.
            // The live join is therefore the renewed token's revision, not the
            // older pre-reservation authority fingerprint in custody.
            || currentAuthority.CapacityRevision
                != token.WarehouseCapacityRevision
            || currentAuthority.MassAuthorityRevision != token.CatalogRevision)
        {
            return FailPreparedOutputPickup(
                PreparedOutputPickupBoundaryFailureCode.DestinationAuthorityStale,
                authorityFailureDetail);
        }
        return default;
    }

    private bool TryResolvePreparedOutputWarehouseDeliveryPosition(
        string destinationId,
        out Vector2Int deliveryPosition)
    {
        deliveryPosition = default;
        string warehouseId = ParseWarehouseIdOrEmpty(destinationId);
        IWarehouseFacility warehouse = GetWarehouses().SingleOrDefault(value =>
            string.Equals(
                value.PersistentInstanceId.Value,
                warehouseId,
                StringComparison.Ordinal));
        return warehouse is BuildableObject building
            && gridProvider.TryGetGrid(out Grid grid)
            && grid != null
            && WorldItemHaulDestinationAuthority.TryResolveDeliveryCell(
                grid,
                building,
                out deliveryPosition);
    }

    private static PreparedOutputPickupBoundaryResult FailPreparedOutputPickup(
        PreparedOutputPickupBoundaryFailureCode code,
        string detail) => new(code, detail);

    internal bool TryPreparePreparedOutputAdmission(
        PreparedOutputExactDestinationAdmissionRequest request,
        out PreparedOutputExactDestinationAdmissionHandle handle,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason)
    {
        handle = default;
        failureCode = PreparedOutputExactDestinationAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!TryValidatePreparedOutputSources(
                request,
                false,
                out WorldItemStackRecord[] sources,
                out string exactLotFingerprint,
                out failureReason))
        {
            failureCode =
                PreparedOutputExactDestinationAdmissionFailureCode.SourceChanged;
            return false;
        }
        if (!TryCapturePreparedOutputAuthority(
                request.TargetAuthority.Kind,
                request.TargetAuthority.DestinationId,
                request.TargetAuthority.Position,
                out PreparedOutputExactDestinationAuthoritySnapshot current,
                out failureCode,
                out failureReason)
            || !AuthoritySnapshotsMatch(current, request.TargetAuthority))
        {
            if (failureCode == PreparedOutputExactDestinationAdmissionFailureCode.None)
            {
                failureCode =
                    PreparedOutputExactDestinationAdmissionFailureCode.AuthorityStale;
                failureReason = "prepared-output destination authority fingerprint stale";
            }
            return false;
        }

        if (current.Kind == PreparedOutputExactDestinationTargetKind.Warehouse)
        {
            IWarehouseFacility warehouse = GetWarehouses().Single(value =>
                string.Equals(
                    WarehouseStorageIdentity.RequireDestinationId(value),
                    current.DestinationId,
                    StringComparison.Ordinal));
            if (warehouse.Inventory.RemainingMassGrams < request.TotalMassGrams)
            {
                return FailPreparedOutputAdmission(
                    PreparedOutputExactDestinationAdmissionFailureCode
                        .CapacityUnavailable,
                    "prepared-output warehouse preflight has insufficient grams",
                    out failureCode,
                    out failureReason);
            }
            for (int index = 0; index < sources.Length; index++)
            {
                DungeonItemDefinition definition;
                try
                {
                    definition = catalog.GetDefinition(sources[index].itemId);
                }
                catch (Exception exception) when (exception is ArgumentException
                                                   or InvalidOperationException
                                                   or KeyNotFoundException)
                {
                    return FailPreparedOutputAdmission(
                        PreparedOutputExactDestinationAdmissionFailureCode
                            .SourceChanged,
                        "prepared-output warehouse item definition changed:"
                            + exception.Message,
                        out failureCode,
                        out failureReason);
                }
                if (definition == null
                    || !warehouse.Inventory.Accepts(definition.StockCategory))
                {
                    return FailPreparedOutputAdmission(
                        PreparedOutputExactDestinationAdmissionFailureCode
                            .CapacityUnavailable,
                        "prepared-output warehouse category rejected",
                        out failureCode,
                        out failureReason);
                }
            }
            handle = new PreparedOutputExactDestinationAdmissionHandle(
                current.Kind,
                default,
                exactLotFingerprint,
                HashPreparedOutputHandle(request, exactLotFingerprint));
            return true;
        }

        FacilityBufferCapacityProfile profile =
            facilityBufferMassAdmission.CaptureProfiles().Single(value =>
                string.Equals(
                    value.DestinationId,
                    current.DestinationId,
                    StringComparison.Ordinal));
        FacilityBufferMassAdmissionRequest exactLot = new(
            request.AdmissionOperationId,
            current.DestinationId,
            current.Position,
            profile.OwnerDomain,
            profile.OwnerOperationId,
            profile.OwnerFacilityId,
            profile.CapacityRevision,
            request.ExactLotSlices.Select((slice, index) =>
                new FacilityBufferMassLotSlice(
                    sources[index].stackId,
                    slice.ExactQuantity,
                    slice.ExpectedSourceReservationRevision,
                    slice.ExpectedComponentFingerprint,
                    slice.ExpectedExactMassGrams)).ToArray());
        FacilityBufferCustodyOwnedAdmissionRequest custodyAdmission = new(
            exactLot,
            request.ExpectedRouteOperationId,
            request.ExpectedPhysicalReceiptFingerprint,
            current.MassAuthorityRevision);
        if (!facilityBufferMassAdmission.TryReserveCustodyOwnedExactLot(
                custodyAdmission,
                out FacilityBufferMassAdmissionToken facilityToken,
                out FacilityBufferMassAdmissionFailureCode facilityFailure,
                out failureReason))
        {
            failureCode = facilityFailure ==
                    FacilityBufferMassAdmissionFailureCode.CapacityUnavailable
                ? PreparedOutputExactDestinationAdmissionFailureCode
                    .CapacityUnavailable
                : PreparedOutputExactDestinationAdmissionFailureCode.ReplayConflict;
            return false;
        }
        if (facilityToken.ReservedMassGrams != request.TotalMassGrams)
        {
            facilityBufferMassAdmission.TryRelease(
                facilityToken,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _);
            return FailPreparedOutputAdmission(
                PreparedOutputExactDestinationAdmissionFailureCode.SourceChanged,
                "prepared-output facility-buffer exact grams changed",
                out failureCode,
                out failureReason);
        }
        handle = new PreparedOutputExactDestinationAdmissionHandle(
            current.Kind,
            facilityToken,
            facilityToken.ExactLot.Fingerprint,
            HashPreparedOutputHandle(request, HashPreparedOutputLot(request)));
        return true;
    }

    internal bool TryRollbackPreparedOutputAdmission(
        PreparedOutputExactDestinationAdmissionHandle handle,
        bool routed,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (handle.Kind == PreparedOutputExactDestinationTargetKind.Warehouse)
            return true;
        if (facilityBufferMassAdmission == null)
        {
            failureReason = "prepared-output facility-buffer admission unavailable";
            return false;
        }
        if (routed)
        {
            if (!facilityBufferMassAdmission.TryGetReceipt(
                    handle.FacilityToken.TokenId,
                    out FacilityBufferMassAdmissionReceipt receipt))
            {
                failureReason = "prepared-output facility-buffer receipt missing";
                return false;
            }
            return facilityBufferMassAdmission.TryRollbackRouted(
                handle.FacilityToken,
                receipt,
                out _,
                out failureReason);
        }
        return facilityBufferMassAdmission.TryRelease(
            handle.FacilityToken,
            FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
            out _,
            out failureReason);
    }

    internal bool TryPublishPreparedOutputAdmission(
        PreparedOutputExactDestinationAdmissionRequest request,
        PreparedOutputExactDestinationAdmissionHandle handle,
        out long committedMassGrams,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason)
    {
        committedMassGrams = 0L;
        failureCode = PreparedOutputExactDestinationAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!TryValidatePreparedOutputSources(
                request,
                true,
                out _,
                out _,
                out failureReason))
        {
            failureCode =
                PreparedOutputExactDestinationAdmissionFailureCode.SourceChanged;
            return false;
        }
        if (handle.Kind == PreparedOutputExactDestinationTargetKind.Warehouse)
            return true;
        if (facilityBufferMassAdmission == null)
        {
            failureCode =
                PreparedOutputExactDestinationAdmissionFailureCode.AuthorityMissing;
            failureReason = "prepared-output facility-buffer admission unavailable";
            return false;
        }
        if (!facilityBufferMassAdmission.TryCommitRouted(
                handle.FacilityToken,
                handle.ExactLotFingerprint,
                handle.FacilityToken.ReservedMassGrams,
                out FacilityBufferMassAdmissionReceipt receipt,
                out FacilityBufferMassAdmissionFailureCode commitFailure,
                out failureReason))
        {
            failureCode = commitFailure ==
                    FacilityBufferMassAdmissionFailureCode.CapacityUnavailable
                ? PreparedOutputExactDestinationAdmissionFailureCode
                    .CapacityUnavailable
                : PreparedOutputExactDestinationAdmissionFailureCode.ReplayConflict;
            return false;
        }
        if (receipt.CommittedMassGrams != handle.FacilityToken.ReservedMassGrams
            || !string.Equals(receipt.ExactLotFingerprint,
                handle.ExactLotFingerprint, StringComparison.Ordinal))
        {
            bool rolledBack = facilityBufferMassAdmission.TryRollbackRouted(
                handle.FacilityToken,
                receipt,
                out _,
                out string rollbackFailure);
            failureCode = rolledBack
                ? PreparedOutputExactDestinationAdmissionFailureCode.ReplayConflict
                : PreparedOutputExactDestinationAdmissionFailureCode.RollbackFailed;
            failureReason = rolledBack
                ? "prepared-output facility-buffer receipt mismatched"
                : "prepared-output facility-buffer receipt mismatch rollback failed:"
                    + rollbackFailure;
            return false;
        }
        committedMassGrams = receipt.CommittedMassGrams;
        return true;
    }

    private bool TryValidatePreparedOutputSources(
        PreparedOutputExactDestinationAdmissionRequest request,
        bool requirePublishedTarget,
        out WorldItemStackRecord[] sources,
        out string exactLotFingerprint,
        out string failureReason)
    {
        sources = Array.Empty<WorldItemStackRecord>();
        exactLotFingerprint = string.Empty;
        failureReason = string.Empty;
        if (!IsCanonicalRequired(request.AdmissionOperationId)
            || !IsCanonicalRequired(request.ExpectedRouteOperationId)
            || !IsCanonicalRequired(request.ExpectedPhysicalReceiptFingerprint)
            || !IsLowerSha256(
                request.ExpectedNextDeliveryRevisionFingerprint)
            || massAdmission == null
            || request.ExactLotSlices.Count == 0
            || request.ExactLotSlices.Select(value => value.SourceStackId)
                .Distinct(StringComparer.Ordinal).Count()
                != request.ExactLotSlices.Count)
        {
            failureReason = "prepared-output custody-owned source changed";
            return false;
        }
        List<WorldItemStackRecord> validated = new(request.ExactLotSlices.Count);
        long validatedMassGrams = 0L;
        foreach (PreparedOutputExactDestinationLotSlice slice in
                 request.ExactLotSlices)
        {
            if (!IsCanonicalRequired(slice.SourceStackId)
                || slice.ExactQuantity <= 0
                || slice.ExpectedSourceReservationRevision < 0L
                || !IsCanonicalRequired(slice.ExpectedComponentFingerprint)
                || slice.ExpectedExactMassGrams <= 0L
                || !repository.RecordsById.TryGetValue(
                    slice.SourceStackId,
                    out WorldItemStackRecord source)
                || source == null
                || source.state != WorldItemStackState.Loose
                || source.quantity != slice.ExactQuantity
                || source.reservationRevision
                    != slice.ExpectedSourceReservationRevision
                || !FacilityOutputExactRouteCustodyCodec.TryRead(
                    source.components,
                    out FacilityOutputExactRouteCustodyMetadata custody)
                || custody.Phase != FacilityOutputExactRouteCustodyPhase.Routable
                || custody.Quantity != source.quantity
                || custody.MassGrams != slice.ExpectedExactMassGrams
                || !string.Equals(custody.CurrentSourceStackId, source.stackId,
                    StringComparison.Ordinal)
                || !string.Equals(custody.RouteOperationId,
                    request.ExpectedRouteOperationId, StringComparison.Ordinal)
                || !string.Equals(custody.PhysicalReceiptFingerprint,
                    request.ExpectedPhysicalReceiptFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(custody.ComponentFingerprint,
                    slice.ExpectedComponentFingerprint, StringComparison.Ordinal)
                || (requirePublishedTarget
                    && (!string.Equals(custody.CurrentTargetDestinationId,
                            request.TargetAuthority.DestinationId,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            custody.CurrentDeliveryRevisionFingerprint,
                            request.ExpectedNextDeliveryRevisionFingerprint,
                            StringComparison.Ordinal))))
            {
                failureReason = "prepared-output custody-owned source changed:"
                    + slice.SourceStackId;
                return false;
            }
            try
            {
                PhysicalItemMassSubject subject = massAdmission.PrepareMassSubject(
                    (ItemDefinitionId)source.itemId,
                    source.itemInstanceId,
                    source.components);
                long unitMassGrams = subject.HasPreparedUnitMass
                    ? subject.PreparedUnitMass.Value
                    : massAdmission.GetDefinitionUnitMassGrams(
                        (ItemDefinitionId)source.itemId);
                long exactMassGrams = checked(unitMassGrams * slice.ExactQuantity);
                if (exactMassGrams != slice.ExpectedExactMassGrams)
                {
                    failureReason =
                        "prepared-output custody-owned mass changed:"
                        + slice.SourceStackId;
                    return false;
                }
                validatedMassGrams = checked(
                    validatedMassGrams + exactMassGrams);
            }
            catch (Exception exception) when (exception is ArgumentException
                                               or InvalidOperationException
                                               or KeyNotFoundException
                                               or OverflowException)
            {
                failureReason = "prepared-output custody-owned mass invalid:"
                    + exception.Message;
                return false;
            }
            validated.Add(source);
        }
        if (validatedMassGrams <= 0L
            || validatedMassGrams != request.TotalMassGrams)
        {
            failureReason = "prepared-output custody-owned total mass changed";
            return false;
        }
        sources = validated.ToArray();
        exactLotFingerprint = HashPreparedOutputLot(request);
        return true;
    }

    private static bool AuthoritySnapshotsMatch(
        PreparedOutputExactDestinationAuthoritySnapshot left,
        PreparedOutputExactDestinationAuthoritySnapshot right) =>
        left.Kind == right.Kind
        && left.Position == right.Position
        && left.CapacityRevision == right.CapacityRevision
        && left.MassAuthorityRevision == right.MassAuthorityRevision
        && left.MaxMassGrams == right.MaxMassGrams
        && string.Equals(
            left.DestinationId,
            right.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(left.Fingerprint, right.Fingerprint,
            StringComparison.Ordinal);

    private static string HashPreparedOutputAuthority(
        string domain,
        string destinationId,
        Vector2Int position,
        long maxMassGrams,
        long capacityRevision,
        long massRevision,
        string policy) => HashLowerSha256(string.Join("|",
        domain,
        destinationId,
        position.x.ToString(CultureInfo.InvariantCulture),
        position.y.ToString(CultureInfo.InvariantCulture),
        maxMassGrams.ToString(CultureInfo.InvariantCulture),
        capacityRevision.ToString(CultureInfo.InvariantCulture),
        massRevision.ToString(CultureInfo.InvariantCulture),
        policy ?? string.Empty));

    private static string HashPreparedOutputHandle(
        PreparedOutputExactDestinationAdmissionRequest request,
        string exactLotFingerprint) => HashLowerSha256(string.Join("|",
        "prepared-output-destination-admission-v1",
        request.ExpectedRouteOperationId,
        request.ExpectedPhysicalReceiptFingerprint,
        request.ExpectedNextDeliveryRevisionFingerprint,
        exactLotFingerprint,
        ((int)request.TargetAuthority.Kind).ToString(
            CultureInfo.InvariantCulture),
        request.TargetAuthority.DestinationId,
        request.TargetAuthority.Position.x.ToString(
            CultureInfo.InvariantCulture),
        request.TargetAuthority.Position.y.ToString(
            CultureInfo.InvariantCulture),
        request.TargetAuthority.MaxMassGrams.ToString(
            CultureInfo.InvariantCulture)));

    private static string HashPreparedOutputLot(
        PreparedOutputExactDestinationAdmissionRequest request) =>
        HashLowerSha256(string.Join("|",
            "prepared-output-exact-lot-v1",
            request.ExpectedRouteOperationId,
            request.ExpectedPhysicalReceiptFingerprint,
            string.Join(",", request.ExactLotSlices.Select(value =>
                value.SourceStackId + ":"
                + value.ExactQuantity.ToString(CultureInfo.InvariantCulture) + ":"
                + value.ExpectedComponentFingerprint + ":"
                + value.ExpectedExactMassGrams.ToString(
                    CultureInfo.InvariantCulture)))));

    private static string HashLowerSha256(string payload)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
        StringBuilder hex = new(digest.Length * 2);
        foreach (byte value in digest)
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return hex.ToString();
    }

    private static bool FailPreparedOutputAdmission(
        PreparedOutputExactDestinationAdmissionFailureCode code,
        string reason,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason)
    {
        failureCode = code;
        failureReason = reason ?? string.Empty;
        return false;
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsLowerSha256(string value) =>
        value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    public bool SpawnStock(
        IWarehouseFacility warehouse,
        StockCategory category,
        int amount,
        out int spawned)
    {
        spawned = 0;
        if (warehouse?.Inventory == null
            || !warehouse.HasWarehouseInventory
            || amount <= 0
            || !warehouse.Inventory.Accepts(category))
        {
            return false;
        }
        DungeonItemDefinition definition = catalog.All
            .Where(candidate => candidate != null
                && candidate.StockCategory == category
                && candidate.MaxStack > 1)
            .OrderBy(candidate => candidate.ItemId, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No authored concrete item belongs to stock category '{category}'.");
        string operationId = AllocateMassIngressOperationId(warehouse);
        bool succeeded = SpawnItemStock(
            warehouse,
            definition.ItemId,
            amount,
            operationId,
            $"generic:{definition.ItemId}",
            out spawned,
            out _,
            out _);
        if (!succeeded && spawned == 0)
        {
            return false;
        }
        return spawned == amount;
    }

    public bool SpawnItemStock(
        IWarehouseFacility warehouse,
        string itemId,
        int amount,
        string ownerOperationId,
        string lotFingerprint,
        out int spawned,
        out WarehouseMassAdmissionReceipt receipt,
        out DomainFailure failure)
    {
        spawned = 0;
        receipt = default;
        failure = DomainFailure.None;
        if (warehouse?.Inventory == null
            || !warehouse.HasWarehouseInventory
            || string.IsNullOrWhiteSpace(itemId)
            || amount <= 0)
        {
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionRequestInvalid,
                ownerOperationId ?? string.Empty,
                itemId ?? string.Empty);
            return false;
        }

        DungeonItemDefinition definition = catalog.GetDefinition(itemId);
        if (!warehouse.Inventory.Accepts(definition.StockCategory))
        {
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionOwnerUnavailable,
                warehouse.PersistentInstanceId.Value,
                StockCategoryPersistenceId.ToId(definition.StockCategory));
            return false;
        }

        if (!warehouse.Inventory.HasMassCapacityAuthority)
        {
            int accepted = warehouse.Inventory.GetAcceptableQuantity(
                definition.ItemId,
                amount);
            spawned = accepted > 0
                ? AddStoredItems(warehouse, definition.ItemId, accepted)
                : 0;
            return spawned == amount;
        }

        if (massAdmission == null)
        {
            throw new InvalidOperationException(
                "A mass-authoritative warehouse requires the admission service.");
        }

        long expectedRevision = massAdmission.GetWarehouseCapacityRevision(
            warehouse.PersistentInstanceId);
        WarehouseMassAdmissionRequest request = new(
            warehouse.PersistentInstanceId,
            ownerOperationId,
            (ItemDefinitionId)definition.ItemId,
            string.Empty,
            lotFingerprint,
            amount,
            expectedRevision,
            massAdmission.CatalogRevision,
            expectedSourceRevision: 0L);
        if (!massAdmission.TryReserve(request, out WarehouseMassAdmissionToken token, out failure))
        {
            return false;
        }

        WarehouseItemMutationUndoJournal undo = CaptureWarehouseItemUndo(
            warehouse,
            definition.ItemId);
        try
        {
            spawned = AddStoredItems(
                warehouse,
                definition.ItemId,
                token.AcceptedQuantity);
            if (spawned != token.AcceptedQuantity)
            {
                RollbackWarehouseItemMutation(warehouse, definition.ItemId, undo);
                massAdmission.TryRelease(
                    token.TokenId,
                    WarehouseMassAdmissionReleaseReason.TransactionRollback,
                    out _);
                failure = new DomainFailure(
                    FailureCode.WarehouseMassAdmissionCommitConflict,
                    token.TokenId,
                    "partial-physical-publication");
                spawned = 0;
                return false;
            }

            string commitId = $"{ownerOperationId}:commit";
            if (!massAdmission.TryCommit(token.TokenId, commitId, out receipt, out failure))
            {
                RollbackWarehouseItemMutation(warehouse, definition.ItemId, undo);
                spawned = 0;
                return false;
            }

            return spawned == amount;
        }
        catch
        {
            RollbackWarehouseItemMutation(warehouse, definition.ItemId, undo);
            massAdmission.TryRelease(
                token.TokenId,
                WarehouseMassAdmissionReleaseReason.TransactionRollback,
                out _);
            spawned = 0;
            throw;
        }
    }

    internal bool TryRenewHaulAdmissions(
        HaulDeliveryIntentSaveData intent,
        out string failureReason)
    {
        failureReason = string.Empty;
        WarehouseHaulAdmissionSaveData[] admissions = intent?.warehouseAdmissions?
            .Where(value => value != null)
            .OrderBy(value => value.ownerAdmissionOperationId, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<WarehouseHaulAdmissionSaveData>();
        if (admissions.Length == 0)
        {
            return true;
        }
        if (massAdmission == null)
        {
            failureReason = "warehouse mass admission service unavailable";
            return false;
        }

        foreach (WarehouseHaulAdmissionSaveData admission in admissions)
        {
            DomainFailure failure = DomainFailure.None;
            BuildingInstanceId warehouseId =
                (BuildingInstanceId)(admission.warehouseId?.Trim() ?? string.Empty);
            if (!warehouseId.IsValid
                || !massAdmission.TryRenew(
                    admission.tokenId,
                    massAdmission.GetWarehouseCapacityRevision(warehouseId),
                    out WarehouseMassAdmissionToken renewed,
                    out failure))
            {
                failureReason =
                    $"warehouse admission renewal failed:{admission?.tokenId}:{failure.Code}";
                return false;
            }

            admission.tokenId = renewed.TokenId;
            admission.reservedMassGrams = renewed.ReservedMassGrams;
            admission.catalogRevision = renewed.CatalogRevision;
            admission.sourceRevision = renewed.SourceRevision;
        }
        return true;
    }

    internal void ReleaseHaulAdmissions(
        HaulDeliveryIntentSaveData intent,
        WarehouseMassAdmissionReleaseReason reason)
    {
        if (massAdmission == null || intent?.warehouseAdmissions == null)
        {
            return;
        }
        foreach (WarehouseHaulAdmissionSaveData admission in intent.warehouseAdmissions)
        {
            if (admission != null && !string.IsNullOrWhiteSpace(admission.tokenId))
            {
                massAdmission.TryRelease(admission.tokenId, reason, out _);
            }
        }
    }

    internal bool TryValidateHaulAdmission(
        HaulDeliveryIntentSaveData intent,
        CharacterCarriedItemSaveData carried,
        IWarehouseFacility warehouse,
        out WarehouseHaulAdmissionSaveData admission,
        out string failureReason)
    {
        admission = null;
        failureReason = string.Empty;
        if (massAdmission == null
            || intent == null
            || carried == null
            || warehouse?.Inventory?.HasMassCapacityAuthority != true)
        {
            failureReason = "warehouse haul admission authority unavailable";
            return false;
        }

        string destinationWarehouseId = warehouse.PersistentInstanceId.Value;
        WarehouseHaulAdmissionSaveData[] matches = (intent.warehouseAdmissions
                ?? new List<WarehouseHaulAdmissionSaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.warehouseId?.Trim(),
                    destinationWarehouseId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.sourceStackId?.Trim(),
                    carried.sourceStackId?.Trim(),
                    StringComparison.Ordinal)
                && string.Equals(value.itemId, carried.itemId, StringComparison.Ordinal)
                && string.Equals(
                    value.itemInstanceId?.Trim(),
                    carried.itemInstanceId?.Trim(),
                    StringComparison.Ordinal)
                && string.Equals(
                    value.lotFingerprint?.Trim(),
                    ItemReservationSignature.Create(
                        carried.itemId,
                        carried.components),
                    StringComparison.Ordinal)
                && value.quantity == carried.quantity)
            .ToArray();
        if (matches.Length != 1
            || !massAdmission.TryGetStatus(
                matches[0].tokenId,
                out WarehouseMassAdmissionStatusSnapshot status)
            || status.Status != WarehouseMassAdmissionTokenStatus.Reserved
            || status.Token.AcceptedQuantity != carried.quantity
            || status.Token.ReservedMassGrams != matches[0].reservedMassGrams)
        {
            failureReason = "warehouse haul admission does not match carried lot";
            return false;
        }

        admission = matches[0];
        return true;
    }

    internal bool TryRebuildRestoredHaulAdmissions(
        HaulDeliveryIntentSaveData intent,
        out string failureReason)
    {
        failureReason = string.Empty;
        WarehouseHaulAdmissionSaveData[] admissions = intent?.warehouseAdmissions?
            .Where(value => value != null)
            .OrderBy(value => value.ownerAdmissionOperationId, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<WarehouseHaulAdmissionSaveData>();
        if (admissions.Length == 0)
        {
            return true;
        }
        if (massAdmission == null
            || intent.destinationKind != WorldItemHaulDestinationKind.Warehouse)
        {
            failureReason = "restored warehouse haul admission authority unavailable";
            return false;
        }

        CharacterCarriedItemSaveData[] physicalCarried = (intent.commitments
                ?? new List<HaulDeliveryItemCommitmentSaveData>())
            .Where(value => value != null)
            .Select(value => repository.RecordsById.TryGetValue(
                    value.carriedStackId?.Trim() ?? string.Empty,
                    out WorldItemStackRecord record)
                && record != null
                ? new CharacterCarriedItemSaveData
                {
                    carriedStackId = record.stackId,
                    sourceStackId = value.sourceStackId,
                    ownerOperationId = intent.operationId,
                    itemInstanceId = record.itemInstanceId,
                    itemId = record.itemId,
                    quantity = record.quantity,
                    wasteOrigin = record.wasteOrigin,
                    contamination = record.contamination,
                    components = (record.components
                            ?? new List<ItemInstanceComponentSaveData>())
                        .Where(component => component != null)
                        .Select(component => component.Clone())
                        .ToList()
                }
                : null)
            .Where(value => value != null)
            .ToArray();
        if (!ExactWarehouseHaulAdmissionJoin.TryValidateSavedIntent(
                intent,
                physicalCarried,
                out failureReason))
        {
            failureReason =
                "restored warehouse haul admission join mismatch:"
                + failureReason;
            return false;
        }

        List<string> rebuiltTokenIds = new();
        void ReleaseRebuiltTokens()
        {
            for (int index = rebuiltTokenIds.Count - 1; index >= 0; index--)
            {
                massAdmission.TryRelease(
                    rebuiltTokenIds[index],
                    WarehouseMassAdmissionReleaseReason.RestoreRollback,
                    out _);
            }
            rebuiltTokenIds.Clear();
        }

        foreach (WarehouseHaulAdmissionSaveData admission in admissions)
        {
            string expectedDestinationId =
                WarehouseStorageIdentity.DestinationPrefix
                + (admission.warehouseId ?? string.Empty);
            IWarehouseFacility warehouse = GetWarehouses().SingleOrDefault(value =>
                value?.Inventory?.HasMassCapacityAuthority == true
                && string.Equals(
                    value.PersistentInstanceId.Value,
                    admission.warehouseId,
                    StringComparison.Ordinal));
            HaulDeliveryItemCommitmentSaveData commitment = intent.commitments?
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.sourceStackId,
                        admission.sourceStackId,
                        StringComparison.Ordinal)
                    && string.Equals(value.itemId, admission.itemId, StringComparison.Ordinal)
                    && value.quantity == admission.quantity
                    && string.Equals(
                        value.expectedStackSignature,
                        admission.lotFingerprint,
                        StringComparison.Ordinal));
            if (warehouse == null
                || commitment == null
                || !string.Equals(
                    intent.destinationId,
                    expectedDestinationId,
                    StringComparison.Ordinal)
                || !ExactWarehouseHaulAdmissionJoin
                    .TryValidateCurrentAuthorityProvenance(
                        admission,
                        massAdmission.CatalogRevision,
                        out _)
                || !repository.RecordsById.TryGetValue(
                    commitment.carriedStackId?.Trim() ?? string.Empty,
                    out WorldItemStackRecord sourceStack)
                || sourceStack == null
                || sourceStack.state != WorldItemStackState.Carried
                || !string.Equals(
                    sourceStack.destinationId,
                    intent.ownerCharacterId,
                    StringComparison.Ordinal)
                || sourceStack.quantity != admission.quantity
                || !string.Equals(
                    sourceStack.itemId,
                    admission.itemId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    sourceStack.itemInstanceId ?? string.Empty,
                    admission.itemInstanceId ?? string.Empty,
                    StringComparison.Ordinal)
                || !string.Equals(
                    ItemReservationSignature.Create(
                        sourceStack.itemId,
                        sourceStack.components),
                    admission.lotFingerprint,
                    StringComparison.Ordinal))
            {
                ReleaseRebuiltTokens();
                failureReason = "restored warehouse haul admission lot mismatch";
                return false;
            }

            PhysicalItemMassSubject massSubject;
            try
            {
                massSubject = massAdmission.PrepareMassSubject(
                    (ItemDefinitionId)sourceStack.itemId,
                    sourceStack.itemInstanceId,
                    sourceStack.components);
            }
            catch (Exception exception)
            {
                ReleaseRebuiltTokens();
                failureReason =
                    $"restored warehouse haul admission mass subject invalid:{exception.Message}";
                return false;
            }

            WarehouseMassAdmissionRequest request = new(
                warehouse.PersistentInstanceId,
                admission.ownerAdmissionOperationId,
                (ItemDefinitionId)admission.itemId,
                admission.itemInstanceId,
                admission.lotFingerprint,
                admission.quantity,
                massAdmission.GetWarehouseCapacityRevision(
                    warehouse.PersistentInstanceId),
                massAdmission.CatalogRevision,
                // SourceRevision is durable provenance, not the ephemeral
                // token id. Preserve it exactly across token reconstruction.
                expectedSourceRevision: admission.sourceRevision,
                massSubject: massSubject);
            bool reserved = massAdmission.TryReserve(
                    request,
                    out WarehouseMassAdmissionToken token,
                    out DomainFailure failure);
            if (!string.IsNullOrEmpty(token.TokenId))
                rebuiltTokenIds.Add(token.TokenId);
            if (!reserved
                || !token.WarehouseId.Equals(warehouse.PersistentInstanceId)
                || !token.ItemId.Equals((ItemDefinitionId)admission.itemId)
                || !string.Equals(token.ItemInstanceId,
                    admission.itemInstanceId, StringComparison.Ordinal)
                || !string.Equals(token.LotFingerprint,
                    admission.lotFingerprint, StringComparison.Ordinal)
                || token.AcceptedQuantity != admission.quantity
                || token.ReservedMassGrams != admission.reservedMassGrams
                || token.CatalogRevision != admission.catalogRevision
                || token.SourceRevision != admission.sourceRevision)
            {
                ReleaseRebuiltTokens();
                failureReason =
                    $"restored warehouse haul admission rebuild failed:{failure.Code}";
                return false;
            }
            // TokenId is intentionally reissued by the current runtime. All
            // stable physical/provenance fields above must remain exact.
            admission.tokenId = token.TokenId;
        }
        return true;
    }

    internal bool TryCommitHaulAdmission(
        WarehouseHaulAdmissionSaveData admission,
        string haulOperationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (massAdmission == null || admission == null)
        {
            failureReason = "warehouse haul admission authority unavailable";
            return false;
        }
        string commitId = $"{haulOperationId}:warehouse-deposit";
        if (!massAdmission.TryCommit(
                admission.tokenId,
                commitId,
                out WarehouseMassAdmissionReceipt receipt,
                out DomainFailure failure)
            || receipt.CommittedQuantity != admission.quantity
            || receipt.CommittedMassGrams != admission.reservedMassGrams)
        {
            failureReason =
                $"warehouse haul admission commit failed:{admission.tokenId}:{failure.Code}";
            return false;
        }
        return true;
    }

    internal bool TryScheduleNextOverCapacityEvacuation(
        IReadOnlyList<string> pendingWarehouseDestinationIds)
    {
        string[] pending = (pendingWarehouseDestinationIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (pending.Length == 0)
        {
            return false;
        }

        HashSet<string> pendingWarehouseIds = pending
            .Select(ParseWarehouseIdOrEmpty)
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        bool hasActiveEvacuationRoute = repository.Records.Any(record => record != null
                && record.quantity > 0
                && record.state == WorldItemStackState.Stored
                && pending.Contains(
                    record.sourceStorageDestinationId?.Trim() ?? string.Empty,
                    StringComparer.Ordinal))
            || repository.HaulDeliveryIntents.CaptureRuntimeState()
                .SelectMany(intent => intent?.warehouseAdmissions
                    ?? new List<WarehouseHaulAdmissionSaveData>())
                .Any(admission => admission != null
                    && pendingWarehouseIds.Contains(
                        admission.sourceWarehouseId?.Trim() ?? string.Empty));
        if (hasActiveEvacuationRoute)
        {
            return false;
        }

        IWarehouseFacility[] warehouses = GetWarehouses()
            .Where(value => value?.Inventory?.HasMassCapacityAuthority == true)
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .ToArray();
        foreach (string sourceDestinationId in pending)
        {
            string sourceWarehouseId = ParseWarehouseIdOrEmpty(sourceDestinationId);
            IWarehouseFacility sourceWarehouse = warehouses.SingleOrDefault(value =>
                string.Equals(
                    value.PersistentInstanceId.Value,
                    sourceWarehouseId,
                    StringComparison.Ordinal));
            if (sourceWarehouse?.Inventory == null)
            {
                continue;
            }

            long excessMass = sourceWarehouse.Inventory.StoredMassGrams
                - sourceWarehouse.Inventory.MaxMassGrams;
            if (excessMass <= 0L)
            {
                repository.ClearPendingWarehouseEvacuation(sourceDestinationId);
                continue;
            }

            WorldItemStackRecord[] sourceStacks = repository.Records
                .Where(record => record != null
                    && record.quantity > 0
                    && record.state == WorldItemStackState.Stored
                    && !record.forbidden
                    && !FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                        record.components)
                    && string.IsNullOrWhiteSpace(record.sourceStorageDestinationId)
                    && string.Equals(
                        record.destinationId,
                        sourceDestinationId,
                        StringComparison.Ordinal)
                    && GetAvailableQuantity(record) > 0)
                .OrderByDescending(record =>
                    massAdmission == null
                        ? 0L
                        : massAdmission.GetDefinitionUnitMassGrams(
                            (ItemDefinitionId)record.itemId))
                .ThenBy(record => record.itemId, StringComparer.Ordinal)
                .ThenBy(record => record.stackId, StringComparer.Ordinal)
                .ToArray();
            foreach (WorldItemStackRecord source in sourceStacks)
            {
                DungeonItemDefinition definition = catalog.GetDefinition(source.itemId);
                long unitMassGrams = massAdmission?.GetDefinitionUnitMassGrams(
                    (ItemDefinitionId)source.itemId) ?? 0L;
                if (unitMassGrams <= 0L)
                {
                    continue;
                }
                IWarehouseFacility target = warehouses
                    .Where(value => !ReferenceEquals(value, sourceWarehouse)
                        && value.Inventory.Accepts(definition.StockCategory)
                        && value.Inventory.GetAcceptableQuantity(source.itemId, 1) == 1
                        && value is BuildableObject building
                        && !building.isDestroy)
                    .OrderBy(value =>
                    {
                        if (sourceWarehouse is not BuildableObject sourceBuilding
                            || value is not BuildableObject targetCandidate)
                        {
                            return int.MaxValue;
                        }
                        Vector2Int delta = targetCandidate.centerPos - sourceBuilding.centerPos;
                        return Math.Abs(delta.x) + Math.Abs(delta.y);
                    })
                    .ThenBy(value => value.Inventory.StoredMassGrams * 1000L
                        / Math.Max(1L, value.Inventory.MaxMassGrams))
                    .ThenBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (target is not BuildableObject targetBuilding)
                {
                    continue;
                }

                int needed = checked((int)Math.Min(
                    int.MaxValue,
                    (excessMass + unitMassGrams - 1L) / unitMassGrams));
                int amount = Math.Min(
                    Math.Min(GetAvailableQuantity(source), needed),
                    target.Inventory.GetAcceptableQuantity(source.itemId, needed));
                if (amount <= 0)
                {
                    continue;
                }
                string targetDestination =
                    WarehouseStorageIdentity.RequireDestinationId(target);
                return TryRequestStackDelivery(
                    source.stackId,
                    amount,
                    targetBuilding.centerPos,
                    targetDestination,
                    out int requested,
                    out _)
                    && requested == amount;
            }
        }
        return false;
    }

    private static string ParseWarehouseIdOrEmpty(string destinationId)
    {
        string value = destinationId?.Trim() ?? string.Empty;
        return value.StartsWith(
                WorldItemStackRuntime.WarehouseStorageDestinationPrefix,
                StringComparison.Ordinal)
            ? value.Substring(WorldItemStackRuntime.WarehouseStorageDestinationPrefix.Length)
            : string.Empty;
    }

    public bool TryRequestDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        requested = 0;
        failureReason = string.Empty;
        int remaining = Mathf.Max(0, amount);
        string destination = destinationId?.Trim() ?? string.Empty;
        if (remaining <= 0)
        {
            return true;
        }
        if (destination.Length == 0)
        {
            failureReason = "items.delivery.destination_missing";
            return false;
        }

        DungeonItemDefinition definition = catalog.GetDefinition(itemId);
        IWarehouseFacility[] warehouses = GetWarehouses().ToArray();
        int available = CountLooseAvailable(definition.ItemId)
            + warehouses.Sum(warehouse =>
                CountUnassignedStored(warehouse, definition.ItemId));
        if (available < remaining)
        {
            failureReason = "items.delivery.stock_unavailable";
            return false;
        }

        if (!TryRetargetDeliveryAtomically(
                warehouses,
                definition.ItemId,
                remaining,
                destinationPosition,
                destination,
                out requested,
                out failureReason))
        {
            return false;
        }
        // A requested delivery is already committed to a concrete facility
        // destination. Mark every created slice as priority immediately so an
        // unrelated warehouse tidy-up cannot starve surgery, plumbing, or any
        // other live facility input indefinitely.
        if (requested > 0)
        {
            PrioritizeDestination(destination);
        }
        if (requested <= 0)
        {
            failureReason = "items.delivery.stock_unavailable";
            return false;
        }
        if (requested < amount)
        {
            failureReason = "items.delivery.partial_request";
            return false;
        }
        return true;
    }

    private bool TryRetargetDeliveryAtomically(
        IReadOnlyList<IWarehouseFacility> warehouses,
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        requested = 0;
        failureReason = string.Empty;
        int remaining = amount;
        List<DeliveryRetargetSlice> plan = new();

        foreach (WorldItemStackRecord source in repository.Records
                     .Where(record => record != null
                         && record.quantity > 0
                         && record.state == WorldItemStackState.Loose
                         && !record.forbidden
                         && !FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                             record.components)
                         && GetAvailableQuantity(record) > 0
                         && string.IsNullOrWhiteSpace(record.destinationId)
                         && string.Equals(record.itemId, itemId, StringComparison.Ordinal))
                     .OrderBy(record => Manhattan(record.position, destinationPosition))
                     .ThenBy(record => record.stackId, StringComparer.Ordinal))
        {
            if (remaining <= 0)
                break;
            int quantity = Mathf.Min(remaining, GetAvailableQuantity(source));
            plan.Add(new DeliveryRetargetSlice
            {
                Source = source,
                Quantity = quantity
            });
            remaining -= quantity;
        }

        foreach (IWarehouseFacility warehouse in (warehouses
                     ?? Array.Empty<IWarehouseFacility>())
                 .Where(candidate => candidate?.Inventory != null)
                 .OrderBy(candidate => candidate is BuildableObject building
                     ? Manhattan(building.centerPos, destinationPosition)
                     : int.MaxValue)
                 .ThenBy(
                     candidate => WarehouseStorageIdentity.RequireDestinationId(candidate),
                     StringComparer.Ordinal))
        {
            if (remaining <= 0)
                break;
            string storageId = WarehouseStorageIdentity.RequireDestinationId(warehouse);
            foreach (WorldItemStackRecord source in repository.Records
                         .Where(record => record != null
                             && record.quantity > 0
                             && record.state == WorldItemStackState.Stored
                             && !record.forbidden
                             && !FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                                 record.components)
                             && GetAvailableQuantity(record) > 0
                             && string.IsNullOrWhiteSpace(record.sourceStorageDestinationId)
                             && string.Equals(record.itemId, itemId, StringComparison.Ordinal)
                             && string.Equals(record.destinationId, storageId, StringComparison.Ordinal))
                         .OrderBy(record => record.stackId, StringComparer.Ordinal))
            {
                if (remaining <= 0)
                    break;
                int quantity = Mathf.Min(remaining, GetAvailableQuantity(source));
                plan.Add(new DeliveryRetargetSlice
                {
                    Source = source,
                    Quantity = quantity,
                    SourceStorageDestinationId = storageId
                });
                remaining -= quantity;
            }
        }

        if (remaining != 0 || plan.Count == 0)
        {
            failureReason = "items.delivery.stock_changed_before_commit";
            return false;
        }

        return TryCommitDeliveryRetargetPlan(
            plan,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out failureReason);
    }

    private bool TryCommitDeliveryRetargetPlan(
        IReadOnlyList<DeliveryRetargetSlice> plan,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        requested = 0;
        failureReason = string.Empty;
        if (amount <= 0
            || plan == null
            || plan.Count == 0
            || plan.Sum(slice => slice?.Quantity ?? 0) != amount
            || plan.Any(slice => slice?.Source == null
                || slice.Quantity <= 0
                || slice.Quantity > GetAvailableQuantity(slice.Source)
                || FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    slice.Source.components)
                || !repository.RecordsById.TryGetValue(
                    slice.Source.stackId,
                    out WorldItemStackRecord current)
                || !ReferenceEquals(current, slice.Source)))
        {
            failureReason = "items.delivery.stock_changed_before_commit";
            return false;
        }

        bool requiresMassAdmission = destinationId.StartsWith(
                ReservedTargetDestinationIdentity.PowerFuelPrefix,
                StringComparison.Ordinal)
            || destinationId.StartsWith(
                ReservedTargetDestinationIdentity.ProductionInputPrefix,
                StringComparison.Ordinal);
        FacilityBufferMassCapacitySnapshot capacity = default;
        bool hasMassAdmission = facilityBufferMassAdmission != null
            && facilityBufferMassAdmission.TryGetCapacity(
                destinationId,
                destinationPosition,
                out capacity);
        if (requiresMassAdmission && !hasMassAdmission)
        {
            failureReason = "items.delivery.facility_buffer_mass_profile_missing";
            return false;
        }
        FacilityBufferMassAdmissionToken admissionToken = default;
        string lotFingerprint = string.Empty;
        long plannedMassGrams = 0L;
        if (hasMassAdmission)
        {
            string transferOperationId =
                $"facility-buffer-retarget:{nextFacilityBufferAdmissionSequence:D12}";
            FacilityBufferCapacityProfile profile = capacity.Profile;
            FacilityBufferMassAdmissionRequest admissionRequest = new(
                transferOperationId,
                destinationId,
                destinationPosition,
                profile.OwnerDomain,
                profile.OwnerOperationId,
                profile.OwnerFacilityId,
                profile.CapacityRevision,
                plan.Select(slice => new FacilityBufferMassLotSlice(
                        slice.Source.stackId,
                        slice.Quantity,
                        slice.Source.reservationRevision))
                    .ToArray());
            if (!facilityBufferMassAdmission.TryReserveExactLot(
                    admissionRequest,
                    out admissionToken,
                    out FacilityBufferMassAdmissionFailureCode admissionFailure,
                    out string admissionReason))
            {
                failureReason =
                    $"items.delivery.facility_buffer_mass:{admissionFailure}:{admissionReason}";
                return false;
            }
            nextFacilityBufferAdmissionSequence = checked(
                nextFacilityBufferAdmissionSequence + 1L);
            lotFingerprint = admissionToken.ExactLot.Fingerprint;
            plannedMassGrams = admissionToken.ReservedMassGrams;
        }

        // A rejected admission must not consume deterministic stack ids.
        // Allocate split identities only after the exact lot has reserved its
        // destination grams and immediately before the reversible mutation.
        Dictionary<DeliveryRetargetSlice, string> splitIds = new();
        foreach (DeliveryRetargetSlice slice in plan)
        {
            if (slice.Quantity < slice.Source.quantity)
            {
                if (!string.IsNullOrWhiteSpace(slice.Source.itemInstanceId))
                {
                    if (hasMassAdmission)
                    {
                        facilityBufferMassAdmission.TryRelease(
                            admissionToken,
                            FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                            out _,
                            out _);
                    }
                    failureReason = "items.delivery.unique_stack_split_forbidden";
                    return false;
                }
                splitIds.Add(slice, repository.AllocateStackId());
            }
        }

        HashSet<Vector2Int> touchedPositions = new();
        DeliveryRetargetRecordSnapshot[] undo = plan
            .Select(slice => slice.Source)
            .Distinct()
            .Select(CaptureDeliveryRetargetRecord)
            .ToArray();
        List<WorldItemStackRecord> added = new();
        List<string> routedStackIds = new();
        FacilityBufferMassAdmissionReceipt routedReceipt = default;
        bool routedCommitted = false;
        try
        {
            foreach (DeliveryRetargetSlice slice in plan)
            {
                WorldItemStackRecord target;
                if (slice.Quantity == slice.Source.quantity)
                {
                    target = slice.Source;
                }
                else
                {
                    slice.Source.quantity -= slice.Quantity;
                    target = CloneRetargetedSlice(
                        slice.Source,
                        splitIds[slice],
                        slice.Quantity);
                    repository.Add(target);
                    added.Add(target);
                }

                target.state = slice.Source.state;
                target.destinationId = destinationId;
                target.sourceStorageDestinationId = slice.SourceStorageDestinationId;
                target.hasDestinationPosition = true;
                target.destinationPosition = destinationPosition;
                target.reservedByPersistentId = string.Empty;
                target.reservedQuantity = 0;
                target.aggregationCohortId = string.Empty;
                ClearRecoveryProvenance(target);
                touchedPositions.Add(target.position);
                routedStackIds.Add(target.stackId);
                requested = checked(requested + slice.Quantity);
            }
            if (hasMassAdmission
                && !facilityBufferMassAdmission.TryCommitRouted(
                    admissionToken,
                    lotFingerprint,
                    plannedMassGrams,
                    out routedReceipt,
                    out FacilityBufferMassAdmissionFailureCode commitFailure,
                    out string commitReason))
            {
                RollbackDeliveryRetarget(undo, added, touchedPositions);
                requested = 0;
                facilityBufferMassAdmission.TryRelease(
                    admissionToken,
                    FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                    out _,
                    out _);
                failureReason =
                    $"items.delivery.facility_buffer_commit:{commitFailure}:{commitReason}";
                return false;
            }
            routedCommitted = hasMassAdmission;
        }
        catch
        {
            RollbackDeliveryRetarget(undo, added, touchedPositions);
            requested = 0;
            if (hasMassAdmission)
            {
                if (routedCommitted)
                {
                    facilityBufferMassAdmission.TryRollbackRouted(
                        admissionToken,
                        routedReceipt,
                        out _,
                        out _);
                }
                else
                {
                    facilityBufferMassAdmission.TryRelease(
                        admissionToken,
                        FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                        out _,
                        out _);
                }
            }
            throw;
        }
        repository.MarkChanged();
        foreach (Vector2Int position in touchedPositions)
            markers.RefreshAt(position);
        foreach (string routedStackId in routedStackIds
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            reservations.PrioritizeHaul(routedStackId);
        }
        return requested == amount;
    }

    private static DeliveryRetargetRecordSnapshot CaptureDeliveryRetargetRecord(
        WorldItemStackRecord record) => new()
    {
        Record = record,
        Quantity = record.quantity,
        State = record.state,
        DestinationId = record.destinationId,
        SourceStorageDestinationId = record.sourceStorageDestinationId,
        HasDestinationPosition = record.hasDestinationPosition,
        DestinationPosition = record.destinationPosition,
        ReservedByPersistentId = record.reservedByPersistentId,
        ReservedQuantity = record.reservedQuantity,
        AggregationCohortId = record.aggregationCohortId,
        DropDisposition = record.dropDisposition,
        RecoveryOwnerOperationId = record.recoveryOwnerOperationId,
        RecoverySourceStackId = record.recoverySourceStackId,
        RecoveryCarrierPersistentId = record.recoveryCarrierPersistentId,
        RecoveryInterruptionKind = record.recoveryInterruptionKind,
        DroppedAtGameTime = record.droppedAtGameTime,
        RecoveryDeadlineGameTime = record.recoveryDeadlineGameTime
    };

    private void RollbackDeliveryRetarget(
        IReadOnlyList<DeliveryRetargetRecordSnapshot> undo,
        IReadOnlyList<WorldItemStackRecord> added,
        ISet<Vector2Int> touchedPositions)
    {
        foreach (WorldItemStackRecord record in added.Reverse())
        {
            if (repository.RecordsById.ContainsKey(record.stackId))
                repository.Remove(record);
        }
        foreach (DeliveryRetargetRecordSnapshot snapshot in undo)
        {
            WorldItemStackRecord record = snapshot.Record;
            record.quantity = snapshot.Quantity;
            record.state = snapshot.State;
            record.destinationId = snapshot.DestinationId;
            record.sourceStorageDestinationId = snapshot.SourceStorageDestinationId;
            record.hasDestinationPosition = snapshot.HasDestinationPosition;
            record.destinationPosition = snapshot.DestinationPosition;
            record.reservedByPersistentId = snapshot.ReservedByPersistentId;
            record.reservedQuantity = snapshot.ReservedQuantity;
            record.aggregationCohortId = snapshot.AggregationCohortId;
            record.dropDisposition = snapshot.DropDisposition;
            record.recoveryOwnerOperationId = snapshot.RecoveryOwnerOperationId;
            record.recoverySourceStackId = snapshot.RecoverySourceStackId;
            record.recoveryCarrierPersistentId =
                snapshot.RecoveryCarrierPersistentId;
            record.recoveryInterruptionKind = snapshot.RecoveryInterruptionKind;
            record.droppedAtGameTime = snapshot.DroppedAtGameTime;
            record.recoveryDeadlineGameTime = snapshot.RecoveryDeadlineGameTime;
            touchedPositions.Add(record.position);
        }
        repository.MarkChanged();
        foreach (Vector2Int position in touchedPositions)
            markers.RefreshAt(position);
    }

    private static WorldItemStackRecord CloneRetargetedSlice(
        WorldItemStackRecord source,
        string stackId,
        int quantity) => new()
    {
        stackId = stackId,
        itemId = source.itemId,
        quantity = quantity,
        state = source.state,
        position = source.position,
        reservationRevision = source.reservationRevision,
        forbidden = source.forbidden,
        sourceCharacterId = source.sourceCharacterId,
        sourceDisplayName = source.sourceDisplayName,
        sourceSpeciesTag = source.sourceSpeciesTag,
        sourceDeathReason = source.sourceDeathReason,
        emergencyButcheryAllowed = source.emergencyButcheryAllowed,
        wasteOrigin = source.wasteOrigin,
        contamination = source.contamination,
        components = (source.components ?? new List<ItemInstanceComponentSaveData>())
            .Where(component => component != null)
            .Select(component => component.Clone())
            .ToList()
    };

    private static void ClearRecoveryProvenance(WorldItemStackRecord target)
    {
        target.dropDisposition = WorldItemDropDisposition.None;
        target.recoveryOwnerOperationId = string.Empty;
        target.recoverySourceStackId = string.Empty;
        target.recoveryCarrierPersistentId = string.Empty;
        target.recoveryInterruptionKind = WorldItemCarryInterruptionKind.None;
        target.droppedAtGameTime = 0d;
        target.recoveryDeadlineGameTime = 0d;
    }

    public bool TryRequestCategoryDelivery(
        StockCategory category,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        requested = 0;
        failureReason = string.Empty;
        int required = Mathf.Max(0, amount);
        string destination = destinationId?.Trim() ?? string.Empty;
        if (required == 0)
        {
            return true;
        }
        if (destination.Length == 0)
        {
            failureReason = "items.delivery.destination_missing";
            return false;
        }

        IWarehouseFacility[] warehouses = GetWarehouses().ToArray();
        var candidates = repository.Records
            .Where(record => record != null
                && record.quantity > 0
                && !record.forbidden
                && GetAvailableQuantity(record) > 0
                && string.IsNullOrWhiteSpace(record.sourceStorageDestinationId)
                && (record.state == WorldItemStackState.Loose
                    && string.IsNullOrWhiteSpace(record.destinationId)
                    || record.state == WorldItemStackState.Stored
                    && string.IsNullOrWhiteSpace(record.destinationId)
                    || record.state == WorldItemStackState.Stored
                    && record.destinationId.StartsWith(
                        WorldItemStackRuntime.WarehouseStorageDestinationPrefix,
                        StringComparison.Ordinal)))
            .GroupBy(record => record.itemId, StringComparer.Ordinal)
            .Select(group => new
            {
                ItemId = group.Key,
                Category = catalog.GetDefinition(group.Key).StockCategory,
                Available = CountLooseAvailable(group.Key)
                    + warehouses.Sum(warehouse =>
                        CountUnassignedStored(warehouse, group.Key))
            })
            .Where(candidate => candidate.Category == category
                && candidate.Available > 0)
            .OrderByDescending(candidate => candidate.Available)
            .ThenBy(candidate => candidate.ItemId, StringComparer.Ordinal)
            .ToArray();

        if (candidates.Sum(candidate => candidate.Available) < required)
        {
            failureReason = "items.delivery.stock_unavailable";
            return false;
        }

        int remaining = required;
        foreach (var candidate in candidates)
        {
            int take = Mathf.Min(remaining, candidate.Available);
            if (!TryRequestDelivery(
                    candidate.ItemId,
                    take,
                    destinationPosition,
                    destination,
                    out int concreteRequested,
                    out failureReason))
            {
                return false;
            }

            requested += concreteRequested;
            remaining -= concreteRequested;
            if (remaining <= 0)
            {
                return true;
            }
        }

        failureReason = "items.delivery.stock_unavailable";
        return false;
    }

    public bool TryReserveStoredForDirectPickup(
        CharacterActor actor,
        string itemId,
        int quantity,
        out WorldItemReservedStackQuantity reservation,
        out Vector2Int pickupStandPosition,
        out string failureReason)
    {
        reservation = default;
        pickupStandPosition = default;
        failureReason = string.Empty;
        if (actor == null
            || string.IsNullOrWhiteSpace(itemId)
            || quantity <= 0
            || !gridProvider.TryGetGrid(out Grid grid))
        {
            failureReason = "items.pickup.invalid_request";
            return false;
        }

        string actorId = characterIds.GetOrAssignPersistentId(actor);
        WorldItemStackRecord selected = repository.Records
            .Where(record => record != null
                && record.quantity > 0
                && record.state == WorldItemStackState.Stored
                && !record.forbidden
                && GetAvailableQuantity(record) > 0
                && string.Equals(record.itemId, itemId, StringComparison.Ordinal))
            .Select(record => new
            {
                Record = record,
                HasStand = TryResolvePickupStandCell(
                    grid,
                    record.position,
                    out Vector2Int stand),
                Stand = stand
            })
            .Where(candidate => candidate.HasStand)
            .OrderBy(candidate => Manhattan(actor.GetNowXY(), candidate.Stand))
            .Select(candidate => candidate.Record)
            .FirstOrDefault();
        if (selected == null
            || !TryResolvePickupStandCell(
                grid,
                selected.position,
                out pickupStandPosition))
        {
            failureReason = "items.pickup.stored_item_unavailable";
            return false;
        }
        string ownerOperationId = $"equipment-pickup:{actorId}:{itemId}";
        ItemQuantityLease quantityLease = null;
        bool reserved = quantityReservations != null
            ? quantityReservations.TryReserve(
                ownerOperationId,
                actorId,
                ItemReservationPurpose.Equipment,
                $"equipment:{actorId}",
                new ItemQuantityReservationRequest(
                    new ItemStackId(selected.stackId),
                    Mathf.Min(
                        GetAvailableQuantity(selected),
                        Mathf.Max(1, quantity)),
                    ItemReservationSignature.Create(selected.itemId, selected.components)),
                out quantityLease,
                out _)
            : reservations.TryReserve(new[] { selected.stackId }, actorId);
        if (!reserved)
        {
            failureReason = "items.pickup.reservation_changed";
            return false;
        }

        if (string.IsNullOrWhiteSpace(selected.sourceStorageDestinationId))
        {
            selected.sourceStorageDestinationId = selected.destinationId;
            selected.destinationId =
                WorldItemStackRuntime.CombatLoadoutDestinationPrefix + actorId;
            selected.hasDestinationPosition = true;
            selected.destinationPosition = actor.GetNowXY();
        }
        reservation = new WorldItemReservedStackQuantity(
            selected.stackId,
            selected.itemId,
            Mathf.Min(
                GetAvailableQuantity(selected) + (quantityLease?.originalQuantity ?? 0),
                Mathf.Max(1, quantity)),
            selected.position,
            WorldItemHaulDestinationKind.Warehouse,
            selected.destinationId,
            quantityLease?.leaseId,
            quantityLease != null ? ownerOperationId : string.Empty);
        repository.MarkChanged();
        markers.RefreshAt(selected.position);
        return true;
    }

    public bool TryRequestStackDelivery(
        string stackId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        requested = 0;
        failureReason = string.Empty;
        string id = stackId?.Trim() ?? string.Empty;
        string destination = destinationId?.Trim() ?? string.Empty;
        if (id.Length > 0
            && repository.RecordsById.TryGetValue(
                id,
                out WorldItemStackRecord protectedSource)
            && protectedSource != null
            && FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                protectedSource.components))
        {
            failureReason =
                "items.delivery.prepared_output_exact_route_required";
            return false;
        }
        if (id.Length == 0
            || destination.Length == 0
            || amount <= 0
            || !repository.RecordsById.TryGetValue(
                id,
                out WorldItemStackRecord source)
            || source == null
            || source.quantity <= 0
            || source.forbidden
            || GetAvailableQuantity(source) <= 0
            || !string.IsNullOrWhiteSpace(source.sourceStorageDestinationId)
            || !string.IsNullOrWhiteSpace(source.destinationId)
                && source.state is not (WorldItemStackState.Stored
                    or WorldItemStackState.FacilityOutputBuffer))
        {
            failureReason = "items.delivery.stack_unavailable";
            return false;
        }
        if (source.state is not (WorldItemStackState.Loose
                or WorldItemStackState.Stored
                or WorldItemStackState.FacilityOutputBuffer))
        {
            failureReason = "items.delivery.stack_state_invalid";
            return false;
        }
        int available = GetAvailableQuantity(source);
        int moved = Mathf.Min(amount, available);
        if (destination.StartsWith(
                ReservedTargetDestinationIdentity.PowerFuelPrefix,
                StringComparison.Ordinal))
        {
            DeliveryRetargetSlice[] exactPlan =
            {
                new()
                {
                    Source = source,
                    Quantity = moved,
                    SourceStorageDestinationId =
                        source.state == WorldItemStackState.Stored
                            ? source.destinationId
                            : string.Empty
                }
            };
            return TryCommitDeliveryRetargetPlan(
                exactPlan,
                moved,
                destinationPosition,
                destination,
                out requested,
                out failureReason);
        }

        Vector2Int sourcePosition = source.position;
        string storageDestination = source.state == WorldItemStackState.Stored
            ? source.destinationId
            : string.Empty;
        if (moved == source.quantity)
        {
            source.state = source.state == WorldItemStackState.FacilityOutputBuffer
                ? WorldItemStackState.Loose
                : source.state;
            source.destinationId = destination;
            source.sourceStorageDestinationId = storageDestination;
            source.hasDestinationPosition = true;
            source.destinationPosition = destinationPosition;
            repository.MarkChanged();
            markers.RefreshAt(sourcePosition);
            requested = moved;
            reservations.PrioritizeHaul(source.stackId);
            return true;
        }

        source.quantity -= moved;
        repository.MarkChanged();
        if (source.quantity <= 0)
        {
            repository.Remove(source);
        }
        requested = spawner.Spawn(
            source.itemId,
            moved,
            sourcePosition,
            source.state == WorldItemStackState.FacilityOutputBuffer
                ? WorldItemStackState.Loose
                : source.state,
            destination,
            true,
            destinationPosition,
            sourceStorageDestinationId: storageDestination,
            wasteOrigin: source.wasteOrigin,
            contamination: source.contamination,
            components: source.components);
        if (requested < moved)
        {
            spawner.Spawn(
                source.itemId,
                moved - requested,
                sourcePosition,
                source.state,
                storageDestination,
                wasteOrigin: source.wasteOrigin,
                contamination: source.contamination,
                components: source.components);
        }
        markers.RefreshAt(sourcePosition);
        if (requested <= 0)
        {
            failureReason = "items.delivery.stack_request_failed";
            return false;
        }
        return requested == amount;
    }

    public void NormalizeStorageIds()
    {
        IWarehouseFacility[] warehouses = GetWarehouses().ToArray();
        foreach (WorldItemStackRecord stack in repository.Records)
        {
            if (stack == null || stack.state != WorldItemStackState.Stored)
            {
                continue;
            }
            stack.destinationId = NormalizeStorageId(
                stack.destinationId,
                stack.position,
                warehouses);
            stack.sourceStorageDestinationId = NormalizeStorageId(
                stack.sourceStorageDestinationId,
                stack.position,
                warehouses);
        }
    }

    public void PrioritizeDestination(string destinationId)
    {
        string destination = destinationId?.Trim() ?? string.Empty;
        if (destination.Length == 0)
        {
            return;
        }
        foreach (WorldItemStackRecord record in repository.Records
                     .Where(record => record != null
                         && string.Equals(
                             record.destinationId,
                             destination,
                             StringComparison.Ordinal)))
        {
            reservations.PrioritizeHaul(record.stackId);
        }
    }

    private int CountLooseAvailable(string itemId)
    {
        return string.IsNullOrWhiteSpace(itemId)
            ? 0
            : repository.Records
                .Where(stack => stack != null
                    && stack.quantity > 0
                    && stack.state == WorldItemStackState.Loose
                    && !stack.forbidden
                    && !FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                        stack.components)
                    && GetAvailableQuantity(stack) > 0
                    && string.IsNullOrWhiteSpace(stack.destinationId)
                    && string.Equals(
                        stack.itemId,
                        itemId,
                        StringComparison.Ordinal))
                .Sum(GetAvailableQuantity);
    }

    private int CountUnassignedStored(
        IWarehouseFacility warehouse,
        string itemId)
    {
        if (warehouse == null || string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }
        string storageId = WarehouseStorageIdentity.RequireDestinationId(warehouse);
        return repository.Records
            .Where(stack => stack != null
                && stack.quantity > 0
                && stack.state == WorldItemStackState.Stored
                && !stack.forbidden
                && !FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    stack.components)
                && GetAvailableQuantity(stack) > 0
                && string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
                && string.Equals(stack.itemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    stack.destinationId ?? string.Empty,
                    storageId,
                    StringComparison.Ordinal))
            .Sum(GetAvailableQuantity);
    }

    private int AddStoredItems(
        IWarehouseFacility warehouse,
        string itemId,
        int amount,
        WasteOriginKind wasteOrigin = WasteOriginKind.Unknown,
        float contamination = 0f,
        IReadOnlyList<ItemInstanceComponentSaveData> components = null)
    {
        if (warehouse == null
            || string.IsNullOrWhiteSpace(itemId)
            || amount <= 0)
        {
            return 0;
        }
        Vector2Int position = warehouse is BuildableObject building
            ? building.centerPos
            : Vector2Int.zero;
        return spawner.Spawn(
            itemId,
            amount,
            position,
            WorldItemStackState.Stored,
            WarehouseStorageIdentity.RequireDestinationId(warehouse),
            wasteOrigin: wasteOrigin,
            contamination: contamination,
            components: components);
    }

    private WarehouseItemMutationUndoJournal CaptureWarehouseItemUndo(
        IWarehouseFacility warehouse,
        string itemId)
    {
        string destinationId = WarehouseStorageIdentity.RequireDestinationId(warehouse);
        WarehouseItemMutationUndoJournal journal = new();
        foreach (WorldItemStackRecord record in repository.Records)
        {
            if (record == null
                || record.quantity <= 0
                || record.state != WorldItemStackState.Stored
                || !string.Equals(record.itemId, itemId, StringComparison.Ordinal)
                || !string.Equals(
                    string.IsNullOrWhiteSpace(record.sourceStorageDestinationId)
                        ? record.destinationId
                        : record.sourceStorageDestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            journal.QuantityByStackId.Add(record.stackId, record.quantity);
        }

        return journal;
    }

    private void RollbackWarehouseItemMutation(
        IWarehouseFacility warehouse,
        string itemId,
        WarehouseItemMutationUndoJournal journal)
    {
        if (journal == null)
        {
            throw new ArgumentNullException(nameof(journal));
        }

        string destinationId = WarehouseStorageIdentity.RequireDestinationId(warehouse);
        WorldItemStackRecord[] currentRecords = repository.Records
            .Where(record => record != null
                && record.quantity > 0
                && record.state == WorldItemStackState.Stored
                && string.Equals(record.itemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    string.IsNullOrWhiteSpace(record.sourceStorageDestinationId)
                        ? record.destinationId
                        : record.sourceStorageDestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .ToArray();
        for (int index = 0; index < currentRecords.Length; index++)
        {
            WorldItemStackRecord record = currentRecords[index];
            if (!journal.QuantityByStackId.TryGetValue(
                    record.stackId,
                    out int originalQuantity))
            {
                repository.Remove(record);
                continue;
            }

            if (record.quantity != originalQuantity)
            {
                record.quantity = originalQuantity;
                repository.MarkChanged();
            }
        }
    }

    private string AllocateMassIngressOperationId(IWarehouseFacility warehouse)
    {
        string warehouseId = warehouse?.PersistentInstanceId.Value ?? string.Empty;
        if (warehouseId.Length == 0)
        {
            throw new InvalidOperationException(
                "A persistent warehouse ID is required for mass admission.");
        }

        long sequence = nextMassIngressSequence;
        nextMassIngressSequence = checked(sequence + 1L);
        return $"warehouse-ingress:{warehouseId}:{sequence:D16}";
    }

    private int GetAvailableQuantity(WorldItemStackRecord stack)
    {
        if (stack == null || stack.quantity <= 0)
            return 0;
        return quantityReservations != null
            ? Mathf.Clamp(
                quantityReservations.GetAvailableQuantity(
                    new ItemStackId(stack.stackId)),
                0,
                stack.quantity)
            : Mathf.Max(0, stack.quantity - stack.reservedQuantity);
    }

    private IEnumerable<IWarehouseFacility> GetWarehouses()
    {
        return worldRegistry.Warehouses.Where(warehouse => warehouse != null
            && warehouse.HasWarehouseInventory
            && warehouse.Inventory != null);
    }

    private static string NormalizeStorageId(
        string storageDestinationId,
        Vector2Int storagePosition,
        IReadOnlyList<IWarehouseFacility> warehouses)
    {
        string normalized = storageDestinationId?.Trim() ?? string.Empty;
        if (!normalized.StartsWith(
                WorldItemStackRuntime.WarehouseStorageDestinationPrefix,
                StringComparison.Ordinal))
        {
            return normalized;
        }
        string suffix = normalized.Substring(
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix.Length);
        if (suffix.StartsWith("building:", StringComparison.Ordinal))
        {
            return normalized;
        }
        throw new InvalidOperationException(
            $"Legacy warehouse storage key '{normalized}' cannot be restored in V18.");
    }

    private static bool TryResolvePickupStandCell(
        Grid grid,
        Vector2Int storagePosition,
        out Vector2Int stand)
    {
        if (grid.IsValidGridPos(storagePosition)
            && grid.IsWalkable(storagePosition))
        {
            stand = storagePosition;
            return true;
        }
        return grid.TryFindNearbyWalkablePositionOnSameFloor(
            storagePosition,
            out stand,
            maxDistance: 1);
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
