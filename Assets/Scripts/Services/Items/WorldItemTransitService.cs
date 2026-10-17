using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>Owns in-transit stack completion and mass-admission commit/rollback.</summary>
internal sealed class WorldItemTransitService
{
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IItemMarkerPresenter markerPresenter;
    private readonly WorldItemRepository repository;
    private readonly IWarehouseMassAdmissionService warehouseMassAdmission;
    private readonly IFacilityBufferMassAdmissionService facilityBufferMassAdmission;
    private long facilityTransitAdmissionSequence = 1L;
#if UNITY_EDITOR
    internal Func<bool> DebugFailBeforeFacilityTransitAdmissionCommit;
#endif

    internal WorldItemTransitService(
        IDungeonItemCatalogProvider catalogProvider,
        IItemMarkerPresenter markerPresenter,
        WorldItemRepository repository,
        IWarehouseMassAdmissionService warehouseMassAdmission,
        IFacilityBufferMassAdmissionService facilityBufferMassAdmission)
    {
        this.catalogProvider = catalogProvider;
        this.markerPresenter = markerPresenter;
        this.repository = repository;
        this.warehouseMassAdmission = warehouseMassAdmission;
        this.facilityBufferMassAdmission = facilityBufferMassAdmission;
    }

    public bool TryBeginTransit(
        ItemStackId stackId,
        Vector2Int expectedPosition,
        string transitOwnerId,
        out ItemTransitStackSnapshot stack,
        out DomainFailure failure)
    {
        stack = default;
        failure = DomainFailure.None;
        string ownerId = transitOwnerId?.Trim() ?? string.Empty;
        if (!stackId.IsValid
            || ownerId.Length == 0
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0)
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackUnavailable,
                stackId.Value);
            return false;
        }

        if (record.state is not (WorldItemStackState.Loose
                or WorldItemStackState.FacilityOutputBuffer))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackUnavailable,
                stackId.Value);
            return false;
        }

        if (FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                record.components))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackUnavailable,
                stackId.Value,
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass
                    .ToString());
            return false;
        }

        if (record.reservedQuantity > 0)
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackReserved,
                stackId.Value);
            return false;
        }

        if (Mathf.Abs(record.position.x - expectedPosition.x)
                + Mathf.Abs(record.position.y - expectedPosition.y)
            > 1)
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackOutOfRange,
                stackId.Value);
            return false;
        }

        Vector2Int previousPosition = record.position;
        record.state = WorldItemStackState.InTransit;
        record.destinationId = ownerId;
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = false;
        record.destinationPosition = default;
        record.reservedByPersistentId = string.Empty;
        repository.MarkChanged();
        markerPresenter.RefreshAt(previousPosition);
        stack = CreateTransitSnapshot(record);
        return true;
    }

    public bool TryInspectStackForTransit(
        ItemStackId stackId,
        out ItemTransitStackSnapshot stack)
    {
        stack = default;
        if (!stackId.IsValid
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0)
        {
            return false;
        }

        stack = CreateTransitSnapshot(record);
        return true;
    }

    public void CopyLoadableTransitStackIds(
        Vector2Int position,
        List<ItemStackId> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        if (!repository.RecordsByPosition.TryGetValue(
                position,
                out List<WorldItemStackRecord> records))
        {
            return;
        }

        foreach (WorldItemStackRecord record in records
                     .Where(record => record != null
                          && record.quantity > 0
                          && record.state is WorldItemStackState.Loose
                              or WorldItemStackState.FacilityOutputBuffer
                          && !FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                              record.components)
                          && record.quantity - record.reservedQuantity > 0)
                     .OrderBy(record => record.stackId, StringComparer.Ordinal))
        {
            destination.Add(new ItemStackId(record.stackId));
        }
    }

    public bool TryGetTransitStack(
        ItemStackId stackId,
        string transitOwnerId,
        out ItemTransitStackSnapshot stack)
    {
        stack = default;
        string ownerId = transitOwnerId?.Trim() ?? string.Empty;
        if (!stackId.IsValid
            || ownerId.Length == 0
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0
            || record.state != WorldItemStackState.InTransit
            || !string.Equals(
                record.destinationId,
                ownerId,
                StringComparison.Ordinal))
        {
            return false;
        }

        stack = CreateTransitSnapshot(record);
        return true;
    }

    public bool TryCompleteTransit(
        ItemStackId stackId,
        string transitOwnerId,
        WorldItemStackState destinationState,
        Vector2Int destinationPosition,
        string destinationId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string ownerId = transitOwnerId?.Trim() ?? string.Empty;
        if (!stackId.IsValid
            || ownerId.Length == 0
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.state != WorldItemStackState.InTransit
            || !string.Equals(
                record.destinationId,
                ownerId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorTransitOwnershipMismatch,
                stackId.Value,
                ownerId);
            return false;
        }

        if (destinationState != WorldItemStackState.Loose)
        {
            failure = new DomainFailure(
                FailureCode.ConveyorDestinationUnavailable,
                destinationState.ToString(),
                "dedicated-transit-completion-required");
            return false;
        }

        Vector2Int previousPosition = record.position;
        repository.Relocate(record, destinationPosition);
        record.state = destinationState;
        record.destinationId = string.Empty;
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = false;
        record.destinationPosition = default;
        record.reservedByPersistentId = string.Empty;
        repository.MarkChanged();
        markerPresenter.RefreshAt(previousPosition);
        markerPresenter.RefreshAt(destinationPosition);
        return true;
    }

    public bool TryCompleteTransitToFacilityBuffer(
        ItemStackId stackId,
        string transitOwnerId,
        Vector2Int destinationPosition,
        string destinationId,
        out FacilityBufferMassAdmissionReceipt receipt,
        out DomainFailure failure)
    {
        receipt = default;
        failure = DomainFailure.None;
        string ownerId = transitOwnerId?.Trim() ?? string.Empty;
        string targetDestinationId = destinationId?.Trim() ?? string.Empty;
        if (facilityBufferMassAdmission == null)
        {
            failure = new DomainFailure(
                FailureCode.ConveyorDestinationUnavailable,
                targetDestinationId,
                "facility-buffer-mass-admission-service-missing");
            return false;
        }

        if (!stackId.IsValid
            || ownerId.Length == 0
            || targetDestinationId.Length == 0
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0
            || record.state != WorldItemStackState.InTransit
            || !string.Equals(
                record.destinationId,
                ownerId,
                StringComparison.Ordinal)
            || !facilityBufferMassAdmission.TryGetCapacity(
                targetDestinationId,
                destinationPosition,
                out FacilityBufferMassCapacitySnapshot capacity)
            || capacity.Profile == null)
        {
            failure = new DomainFailure(
                FailureCode.ConveyorDestinationUnavailable,
                stackId.Value,
                targetDestinationId);
            return false;
        }

        FacilityBufferCapacityProfile profile = capacity.Profile;
        string transferOperationId =
            "facility-buffer-transit:"
            + facilityTransitAdmissionSequence.ToString(
                "D12",
                System.Globalization.CultureInfo.InvariantCulture);
        FacilityBufferMassAdmissionRequest request = new(
            transferOperationId,
            targetDestinationId,
            destinationPosition,
            profile.OwnerDomain,
            profile.OwnerOperationId,
            profile.OwnerFacilityId,
            profile.CapacityRevision,
            new[]
            {
                new FacilityBufferMassLotSlice(
                    record.stackId,
                    record.quantity,
                    record.reservationRevision)
            });
        if (!facilityBufferMassAdmission.TryReserveExactLot(
                request,
                out FacilityBufferMassAdmissionToken token,
                out FacilityBufferMassAdmissionFailureCode admissionFailure,
                out string admissionReason))
        {
            failure = MapFacilityTransitAdmissionFailure(
                admissionFailure,
                admissionReason,
                stackId.Value,
                targetDestinationId);
            return false;
        }
        facilityTransitAdmissionSequence = checked(
            facilityTransitAdmissionSequence + 1L);

        Vector2Int previousPosition = record.position;
        WorldItemStackState previousState = record.state;
        string previousDestinationId = record.destinationId;
        string previousSourceStorageDestinationId =
            record.sourceStorageDestinationId;
        bool previousHasDestinationPosition = record.hasDestinationPosition;
        Vector2Int previousDestinationPosition = record.destinationPosition;
        string previousReservedByPersistentId = record.reservedByPersistentId;
        try
        {
            repository.Relocate(record, destinationPosition);
            record.state = WorldItemStackState.FacilityBuffer;
            record.destinationId = targetDestinationId;
            record.sourceStorageDestinationId = string.Empty;
            record.hasDestinationPosition = false;
            record.destinationPosition = default;
            record.reservedByPersistentId = string.Empty;
            repository.MarkChanged();
            markerPresenter.RefreshAt(previousPosition);
            markerPresenter.RefreshAt(destinationPosition);

#if UNITY_EDITOR
            if (DebugFailBeforeFacilityTransitAdmissionCommit?.Invoke() == true)
            {
                repository.Relocate(record, previousPosition);
                record.state = previousState;
                record.destinationId = previousDestinationId;
                record.sourceStorageDestinationId =
                    previousSourceStorageDestinationId;
                record.hasDestinationPosition = previousHasDestinationPosition;
                record.destinationPosition = previousDestinationPosition;
                record.reservedByPersistentId = previousReservedByPersistentId;
                repository.MarkChanged();
                markerPresenter.RefreshAt(destinationPosition);
                markerPresenter.RefreshAt(previousPosition);
                if (!facilityBufferMassAdmission.TryRelease(
                        token,
                        FacilityBufferMassAdmissionReleaseReason
                            .TransactionRollback,
                        out FacilityBufferMassAdmissionFailureCode
                            debugReleaseFailure,
                        out string debugReleaseReason))
                {
                    throw new InvalidOperationException(
                        "Facility-buffer transit debug rollback failed: "
                        + debugReleaseFailure + ":" + debugReleaseReason);
                }
                failure = new DomainFailure(
                    FailureCode.ConveyorDestinationUnavailable,
                    stackId.Value,
                    targetDestinationId,
                    "debug-fail-before-facility-transit-admission-commit");
                return false;
            }
#endif

            if (!facilityBufferMassAdmission.TryCommitRouted(
                    token,
                    token.ExactLot.Fingerprint,
                    token.ReservedMassGrams,
                    out receipt,
                    out FacilityBufferMassAdmissionFailureCode commitFailure,
                    out string commitReason))
            {
                repository.Relocate(record, previousPosition);
                record.state = previousState;
                record.destinationId = previousDestinationId;
                record.sourceStorageDestinationId =
                    previousSourceStorageDestinationId;
                record.hasDestinationPosition = previousHasDestinationPosition;
                record.destinationPosition = previousDestinationPosition;
                record.reservedByPersistentId = previousReservedByPersistentId;
                repository.MarkChanged();
                markerPresenter.RefreshAt(destinationPosition);
                markerPresenter.RefreshAt(previousPosition);
                if (!facilityBufferMassAdmission.TryRelease(
                        token,
                        FacilityBufferMassAdmissionReleaseReason
                            .TransactionRollback,
                        out FacilityBufferMassAdmissionFailureCode releaseFailure,
                        out string releaseReason))
                {
                    throw new InvalidOperationException(
                        "Facility-buffer transit admission release failed: "
                        + releaseFailure + ":" + releaseReason);
                }
                failure = MapFacilityTransitAdmissionFailure(
                    commitFailure,
                    commitReason,
                    stackId.Value,
                    targetDestinationId);
                return false;
            }
            return true;
        }
        catch
        {
            repository.Relocate(record, previousPosition);
            record.state = previousState;
            record.destinationId = previousDestinationId;
            record.sourceStorageDestinationId =
                previousSourceStorageDestinationId;
            record.hasDestinationPosition = previousHasDestinationPosition;
            record.destinationPosition = previousDestinationPosition;
            record.reservedByPersistentId = previousReservedByPersistentId;
            repository.MarkChanged();
            markerPresenter.RefreshAt(destinationPosition);
            markerPresenter.RefreshAt(previousPosition);
            if (facilityBufferMassAdmission.TryGetReceipt(
                    token.TokenId,
                    out FacilityBufferMassAdmissionReceipt routedReceipt))
            {
                if (!facilityBufferMassAdmission.TryRollbackRouted(
                        token,
                        routedReceipt,
                        out FacilityBufferMassAdmissionFailureCode rollbackFailure,
                        out string rollbackReason))
                {
                    throw new InvalidOperationException(
                        "Facility-buffer transit routed rollback failed: "
                        + rollbackFailure + ":" + rollbackReason);
                }
            }
            else if (!facilityBufferMassAdmission.TryRelease(
                         token,
                         FacilityBufferMassAdmissionReleaseReason
                             .TransactionRollback,
                         out FacilityBufferMassAdmissionFailureCode
                             releaseFailure,
                         out string releaseReason))
            {
                throw new InvalidOperationException(
                    "Facility-buffer transit reservation rollback failed: "
                    + releaseFailure + ":" + releaseReason);
            }
            throw;
        }
    }

    private static DomainFailure MapFacilityTransitAdmissionFailure(
        FacilityBufferMassAdmissionFailureCode admissionFailure,
        string admissionReason,
        string stackId,
        string destinationId) =>
        new(
            admissionFailure
                == FacilityBufferMassAdmissionFailureCode.CapacityUnavailable
                    ? FailureCode.ConveyorPortFull
                    : FailureCode.ConveyorDestinationUnavailable,
            stackId ?? string.Empty,
            destinationId ?? string.Empty,
            admissionFailure.ToString(),
            admissionReason ?? string.Empty);

    private static ItemTransitStackSnapshot CreateTransitSnapshot(
        WorldItemStackRecord record) =>
        new ItemTransitStackSnapshot(
            new ItemStackId(record.stackId),
            record.itemId,
            record.quantity,
            record.forbidden,
            record.contamination);

    public bool TryCompleteTransitToWarehouse(
        ItemStackId stackId,
        string transitOwnerId,
        IWarehouseFacility warehouse,
        out WarehouseMassAdmissionReceipt receipt,
        out DomainFailure failure)
    {
        receipt = default;
        failure = DomainFailure.None;
        string ownerId = transitOwnerId?.Trim() ?? string.Empty;
        if (warehouseMassAdmission == null)
        {
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionRequestInvalid,
                ownerId,
                "mass-admission-service-missing");
            return false;
        }

        if (!stackId.IsValid
            || ownerId.Length == 0
            || warehouse == null
            || warehouse.Inventory == null
            || !warehouse.HasWarehouseInventory
            || !warehouse.PersistentInstanceId.IsValid
            || !warehouse.Inventory.HasMassCapacityAuthority
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0
            || record.state != WorldItemStackState.InTransit
            || !string.Equals(
                record.destinationId,
                ownerId,
                StringComparison.Ordinal)
            || !catalogProvider.TryGetDefinition(
                record.itemId,
                out DungeonItemDefinition definition)
            || !warehouse.Inventory.Accepts(definition.StockCategory))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorDestinationUnavailable,
                stackId.Value,
                warehouse?.PersistentInstanceId.Value ?? string.Empty);
            return false;
        }

        BuildingInstanceId warehouseId = warehouse.PersistentInstanceId;
        long expectedCapacityRevision = warehouseMassAdmission
            .GetWarehouseCapacityRevision(warehouseId);
        string operationId = ownerId
            + ":warehouse:"
            + warehouseId.Value
            + ":revision:"
            + expectedCapacityRevision.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        ItemDefinitionId itemId = new(record.itemId);
        PhysicalItemMassSubject massSubject = warehouseMassAdmission
            .PrepareMassSubject(
                itemId,
                record.itemInstanceId,
                record.components);
        WarehouseMassAdmissionRequest request = new(
            warehouseId,
            operationId,
            itemId,
            record.itemInstanceId,
            ItemReservationSignature.Create(record.itemId, record.components),
            record.quantity,
            expectedCapacityRevision,
            warehouseMassAdmission.CatalogRevision,
            repository.ItemStackVersion,
            massSubject);
        if (!warehouseMassAdmission.TryReserve(
                request,
                out WarehouseMassAdmissionToken token,
                out failure))
        {
            return false;
        }

        if (token.AcceptedQuantity != record.quantity)
        {
            if (!warehouseMassAdmission.TryRelease(
                    token.TokenId,
                    WarehouseMassAdmissionReleaseReason.TransactionRollback,
                    out DomainFailure releaseFailure))
            {
                failure = releaseFailure;
                return false;
            }

            failure = new DomainFailure(
                FailureCode.WarehouseMassCapacityUnavailable,
                warehouseId.Value,
                record.itemId,
                token.AcceptedQuantity.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                record.quantity.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            return false;
        }

        Vector2Int previousPosition = record.position;
        WorldItemStackState previousState = record.state;
        string previousDestinationId = record.destinationId;
        string previousSourceStorageDestinationId =
            record.sourceStorageDestinationId;
        bool previousHasDestinationPosition = record.hasDestinationPosition;
        Vector2Int previousDestinationPosition = record.destinationPosition;
        string previousReservedByPersistentId = record.reservedByPersistentId;

        Vector2Int warehousePosition = warehouse is BuildableObject building
            ? building.centerPos
            : Vector2Int.zero;
        repository.Relocate(record, warehousePosition);
        record.state = WorldItemStackState.Stored;
        record.destinationId = WarehouseStorageIdentity
            .RequireDestinationId(warehouse);
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = false;
        record.destinationPosition = default;
        record.reservedByPersistentId = string.Empty;
        repository.MarkChanged();
        markerPresenter.RefreshAt(previousPosition);
        markerPresenter.RefreshAt(warehousePosition);

        string commitId = operationId + ":commit";
        if (warehouseMassAdmission.TryCommit(
                token.TokenId,
                commitId,
                out receipt,
                out failure))
        {
            return true;
        }

        repository.Relocate(record, previousPosition);
        record.state = previousState;
        record.destinationId = previousDestinationId;
        record.sourceStorageDestinationId = previousSourceStorageDestinationId;
        record.hasDestinationPosition = previousHasDestinationPosition;
        record.destinationPosition = previousDestinationPosition;
        record.reservedByPersistentId = previousReservedByPersistentId;
        repository.MarkChanged();
        markerPresenter.RefreshAt(warehousePosition);
        markerPresenter.RefreshAt(previousPosition);
        if (warehouseMassAdmission.TryGetStatus(
                token.TokenId,
                out WarehouseMassAdmissionStatusSnapshot status)
            && status.Status == WarehouseMassAdmissionTokenStatus.Reserved)
        {
            warehouseMassAdmission.TryRelease(
                token.TokenId,
                WarehouseMassAdmissionReleaseReason.TransactionRollback,
                out _);
        }
        return false;
    }
}
