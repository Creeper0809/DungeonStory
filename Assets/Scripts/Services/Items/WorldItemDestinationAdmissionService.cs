using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class WorldItemDestinationAdmissionService
{
    private readonly IDungeonItemCatalogProvider catalog;
    private readonly WorldItemRepository repository;
    private readonly IWarehouseWorldQuery worldRegistry;
    private readonly IGridSystemProvider gridProvider;
    private readonly IWarehouseMassAdmissionService massAdmission;
    private readonly IFacilityBufferMassAdmissionService facilityBufferMassAdmission;

    internal WorldItemDestinationAdmissionService(
        IDungeonItemCatalogProvider catalog,
        WorldItemRepository repository,
        IWarehouseWorldQuery worldRegistry,
        IGridSystemProvider gridProvider,
        IWarehouseMassAdmissionService massAdmission,
        IFacilityBufferMassAdmissionService facilityBufferMassAdmission)
    {
        this.catalog = catalog;
        this.repository = repository;
        this.worldRegistry = worldRegistry;
        this.gridProvider = gridProvider;
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

        string warehouseIdValue = WarehouseStorageIdentity.ParseWarehouseIdOrEmpty(destination);
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
        string expectedWarehouseId = WarehouseStorageIdentity.ParseWarehouseIdOrEmpty(
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
        string warehouseId = WarehouseStorageIdentity.ParseWarehouseIdOrEmpty(destinationId);
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
            if (!massAdmission.TryGetStatus(
                    admission.tokenId,
                    out WarehouseMassAdmissionStatusSnapshot status))
            {
                failureReason =
                    $"warehouse admission renewal failed:{admission.tokenId}:{FailureCode.WarehouseMassAdmissionTokenMissing}";
                return false;
            }

            if (!massAdmission.TryRenew(
                    admission.tokenId,
                    status.Token.WarehouseCapacityRevision,
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
            bool reserved = massAdmission.TryRestoreReserved(
                    admission.tokenId,
                    request,
                    admission.reservedMassGrams,
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
                || !string.Equals(token.TokenId,
                    admission.tokenId, StringComparison.Ordinal)
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

    private IEnumerable<IWarehouseFacility> GetWarehouses()
    {
        return worldRegistry.Warehouses.Where(warehouse => warehouse != null
            && warehouse.HasWarehouseInventory
            && warehouse.Inventory != null);
    }
}
