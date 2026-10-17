using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using UnityEngine;

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

    private readonly WorldItemDestinationAdmissionService destinationAdmission;
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
    private readonly IFacilityBufferDestinationClaimQuery
        facilityBufferDestinationClaims;
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
        IFacilityBufferMassAdmissionService facilityBufferMassAdmission = null,
        IFacilityBufferDestinationClaimQuery
            facilityBufferDestinationClaims = null)
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
        this.facilityBufferDestinationClaims =
            facilityBufferDestinationClaims;
        destinationAdmission = new WorldItemDestinationAdmissionService(
            catalog, repository, worldRegistry, gridProvider,
            massAdmission, facilityBufferMassAdmission);
    }

    internal bool TryCapturePreparedOutputAuthority(
        PreparedOutputExactDestinationTargetKind kind,
        string destinationId,
        Vector2Int position,
        out PreparedOutputExactDestinationAuthoritySnapshot snapshot,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason)
    {
        return destinationAdmission.TryCapturePreparedOutputAuthority(
            kind,
            destinationId,
            position,
            out snapshot,
            out failureCode,
            out failureReason);
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
        return destinationAdmission.ValidatePreparedOutputPickupBoundary(
            reservation,
            lease,
            source,
            pickupQuantity,
            exactMassGrams);
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
        return WorldItemDestinationAdmissionService.ValidatePreparedOutputPickupBoundarySnapshot(
            reservation,
            lease,
            source,
            pickupQuantity,
            exactMassGrams,
            custody,
            intent,
            admission,
            status,
            hasCurrentDeliveryProjection,
            currentDeliveryPosition,
            hasCurrentAuthority,
            currentAuthority,
            authorityFailureDetail);
    }

    internal bool TryPreparePreparedOutputAdmission(
        PreparedOutputExactDestinationAdmissionRequest request,
        out PreparedOutputExactDestinationAdmissionHandle handle,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason)
    {
        return destinationAdmission.TryPreparePreparedOutputAdmission(
            request,
            out handle,
            out failureCode,
            out failureReason);
    }

    internal bool TryRollbackPreparedOutputAdmission(
        PreparedOutputExactDestinationAdmissionHandle handle,
        bool routed,
        out string failureReason)
    {
        return destinationAdmission.TryRollbackPreparedOutputAdmission(
            handle,
            routed,
            out failureReason);
    }

    internal bool TryPublishPreparedOutputAdmission(
        PreparedOutputExactDestinationAdmissionRequest request,
        PreparedOutputExactDestinationAdmissionHandle handle,
        out long committedMassGrams,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason)
    {
        return destinationAdmission.TryPublishPreparedOutputAdmission(
            request,
            handle,
            out committedMassGrams,
            out failureCode,
            out failureReason);
    }

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
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionOwnerUnavailable,
                warehouse.PersistentInstanceId.Value,
                "missing-positive-gram-capacity");
            return false;
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
        return destinationAdmission.TryRenewHaulAdmissions(
            intent,
            out failureReason);
    }

    internal void ReleaseHaulAdmissions(
        HaulDeliveryIntentSaveData intent,
        WarehouseMassAdmissionReleaseReason reason)
    {
        destinationAdmission.ReleaseHaulAdmissions(
            intent,
            reason);
    }

    internal bool TryValidateHaulAdmission(
        HaulDeliveryIntentSaveData intent,
        CharacterCarriedItemSaveData carried,
        IWarehouseFacility warehouse,
        out WarehouseHaulAdmissionSaveData admission,
        out string failureReason)
    {
        return destinationAdmission.TryValidateHaulAdmission(
            intent,
            carried,
            warehouse,
            out admission,
            out failureReason);
    }

    internal bool TryRebuildRestoredHaulAdmissions(
        HaulDeliveryIntentSaveData intent,
        out string failureReason)
    {
        return destinationAdmission.TryRebuildRestoredHaulAdmissions(
            intent,
            out failureReason);
    }

    internal bool TryCommitHaulAdmission(
        WarehouseHaulAdmissionSaveData admission,
        string haulOperationId,
        out string failureReason)
    {
        return destinationAdmission.TryCommitHaulAdmission(
            admission,
            haulOperationId,
            out failureReason);
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
            .Select(WarehouseStorageIdentity.ParseWarehouseIdOrEmpty)
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
            string sourceWarehouseId = WarehouseStorageIdentity.ParseWarehouseIdOrEmpty(sourceDestinationId);
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

        bool requiresMassAdmission = RequiresFacilityBufferMassAdmission(
                destinationId,
                destinationPosition)
            || destinationId.StartsWith(
                ReservedTargetDestinationIdentity.PowerFuelPrefix,
                StringComparison.Ordinal)
            || destinationId.StartsWith(
                ReservedTargetDestinationIdentity.ProductionInputPrefix,
                StringComparison.Ordinal)
            || destinationId.StartsWith(
                ReservedTargetDestinationIdentity.ProductionStockSensorPrefix,
                StringComparison.Ordinal)
            || destinationId.StartsWith(
                ReservedTargetDestinationIdentity.ExpeditionPrefix,
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

                target.state = slice.Source.state ==
                    WorldItemStackState.FacilityOutputBuffer
                        ? WorldItemStackState.Loose
                        : slice.Source.state;
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
        string actorId = actor != null
            ? characterIds.GetOrAssignPersistentId(actor)
            : string.Empty;
        return TryReserveStoredForDirectPickup(
            actor,
            itemId,
            quantity,
            ItemReservationPurpose.Equipment,
            $"equipment-pickup:{actorId}:{itemId}",
            out reservation,
            out pickupStandPosition,
            out failureReason);
    }

    [GameplayInternalOnly(
        "Exact stored direct-pickup flows reserve under the caller's persisted operation identity.",
        "WorldItemStackRuntime")]
    internal bool TryReserveStoredForDirectPickup(
        CharacterActor actor,
        string itemId,
        int quantity,
        ItemReservationPurpose purpose,
        string ownerOperationId,
        out WorldItemReservedStackQuantity reservation,
        out Vector2Int pickupStandPosition,
        out string failureReason)
    {
        reservation = default;
        pickupStandPosition = default;
        failureReason = string.Empty;
        string operationId = ownerOperationId?.Trim() ?? string.Empty;
        if (actor == null
            || string.IsNullOrWhiteSpace(itemId)
            || quantity <= 0
            || operationId.Length == 0
            || !Enum.IsDefined(typeof(ItemReservationPurpose), purpose)
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
        ItemQuantityLease quantityLease = null;
        bool reserved = quantityReservations != null
            ? quantityReservations.TryReserve(
                operationId,
                actorId,
                purpose,
                $"direct-pickup:{actorId}",
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
            quantityLease != null ? operationId : string.Empty);
        repository.MarkChanged();
        markers.RefreshAt(selected.position);
        return true;
    }

    [GameplayInternalOnly(
        "The owning direct-pickup adapter releases its exact lease and restores the stored source route.",
        "WorldItemStackRuntime")]
    internal bool TryFinalizeStoredDirectPickup(
        string ownerOperationId,
        string sourceStackId,
        ItemReservationReleaseReason releaseReason,
        out string failureReason)
    {
        failureReason = string.Empty;
        string operation = ownerOperationId?.Trim() ?? string.Empty;
        string stackId = sourceStackId?.Trim() ?? string.Empty;
        if (operation.Length == 0 || stackId.Length == 0)
        {
            failureReason = "items.pickup.finalize_invalid_request";
            return false;
        }

        quantityReservations?.ReleaseByOwner(operation, releaseReason);
        if (quantityReservations != null
            && quantityReservations.TryGetLeasesByOwner(
                operation,
                out IReadOnlyList<ItemQuantityLease> remaining)
            && remaining.Count > 0)
        {
            failureReason = "items.pickup.finalize_lease_still_active";
            return false;
        }

        if (!repository.RecordsById.TryGetValue(
                stackId,
                out WorldItemStackRecord source)
            || source == null
            || source.state != WorldItemStackState.Stored
            || string.IsNullOrWhiteSpace(
                source.sourceStorageDestinationId))
        {
            // A full pickup transferred this record identity to Carried. There
            // is no remaining stored source route to restore in that case.
            return true;
        }
        if (quantityReservations != null
            && quantityReservations.GetReservedQuantity(
                new ItemStackId(stackId)) > 0)
        {
            return true;
        }

        source.destinationId = source.sourceStorageDestinationId.Trim();
        source.sourceStorageDestinationId = string.Empty;
        source.hasDestinationPosition = false;
        source.destinationPosition = default;
        repository.MarkChanged();
        markers.RefreshAt(source.position);
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
        bool hasExactMassProfile = facilityBufferMassAdmission != null
            && facilityBufferMassAdmission.TryGetCapacity(
                destination,
                destinationPosition,
                out _);
        if (hasExactMassProfile
            || RequiresFacilityBufferMassAdmission(
                destination,
                destinationPosition)
            || destination.StartsWith(
                ReservedTargetDestinationIdentity.PowerFuelPrefix,
                StringComparison.Ordinal)
            || destination.StartsWith(
                ReservedTargetDestinationIdentity.ProductionStockSensorPrefix,
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

    private bool RequiresFacilityBufferMassAdmission(
        string destinationId,
        Vector2Int destinationPosition)
    {
        return facilityBufferDestinationClaims != null
            && facilityBufferDestinationClaims.TryGetClaim(
                destinationId,
                destinationPosition,
                out FacilityBufferDestinationClaim claim)
            && claim.AdmissionPolicy
                == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired;
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
