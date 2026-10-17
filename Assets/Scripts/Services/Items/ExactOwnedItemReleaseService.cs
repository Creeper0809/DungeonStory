using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class ExactOwnedItemReleaseService
{
    private readonly IFacilityBufferDestinationClaimQuery destinationClaims;
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IPhysicalItemRelocationService physicalItemRelocations;
    private readonly WorldItemRepository repository;
    private readonly IWarehouseWorldQuery worldRegistry;

    internal ExactOwnedItemReleaseService(
        IFacilityBufferDestinationClaimQuery destinationClaims,
        IGridSystemProvider gridSystemProvider,
        IPhysicalItemRelocationService physicalItemRelocations,
        WorldItemRepository repository,
        IWarehouseWorldQuery worldRegistry)
    {
        this.destinationClaims = destinationClaims;
        this.gridSystemProvider = gridSystemProvider;
        this.physicalItemRelocations = physicalItemRelocations;
        this.repository = repository;
        this.worldRegistry = worldRegistry;
    }

    [GameplayInternalOnly(
        "Releases one exact owner-claimed whole stack to an existing legal outbound cell without changing its identity.",
        "SurgicalPartRuntime freshness expiry only")]
    public bool TryReleaseExactOwnedWholeStack(
        ExactOwnedItemReleaseRequest request,
        out PhysicalItemRelocationReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        if (!IsCanonicalRequired(request.StackId)
            || !IsCanonicalRequired(request.ItemId)
            || !IsCanonicalRequired(request.ItemInstanceId)
            || request.Quantity <= 0
            || request.SourceState is not (WorldItemStackState.Stored
                or WorldItemStackState.FacilityBuffer)
            || !IsCanonicalRequired(request.OwnerDestinationId)
            || !IsCanonicalOptional(request.OwnerDomain)
            || !IsCanonicalRequired(request.OperationId)
            || !IsCanonicalRequired(request.ReasonCode))
        {
            failureReason = "owned-item-release-invalid-request";
            return false;
        }
        if (!repository.RecordsById.TryGetValue(
                request.StackId,
                out WorldItemStackRecord source)
            || source == null
            || source.quantity != request.Quantity
            || source.reservedQuantity != 0
            || !string.IsNullOrEmpty(source.reservedByPersistentId)
            || source.state != request.SourceState
            || !string.Equals(source.itemId, request.ItemId,
                StringComparison.Ordinal)
            || !string.Equals(source.itemInstanceId, request.ItemInstanceId,
                StringComparison.Ordinal)
            || !string.Equals(source.destinationId,
                request.OwnerDestinationId, StringComparison.Ordinal)
            || !string.IsNullOrEmpty(source.sourceStorageDestinationId)
            || FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                source.components))
        {
            failureReason = "owned-item-release-source-mismatch";
            return false;
        }

        bool ownerMatches = request.SourceState switch
        {
            WorldItemStackState.Stored =>
                request.OwnerDomain.Length == 0
                && worldRegistry.Warehouses.Count(warehouse =>
                    WorldItemTransferLocationRules.IsExactLiveWarehouseOwner(
                        warehouse,
                        request.OwnerDestinationId,
                        source.position)) == 1,
            WorldItemStackState.FacilityBuffer =>
                request.OwnerDomain.Length > 0
                && destinationClaims.TryGetClaim(
                    request.OwnerDestinationId,
                    source.position,
                    out FacilityBufferDestinationClaim claim)
                && claim != null
                && claim.AnchorKind ==
                    FacilityBufferDestinationAnchorKind.LiveFacility
                && string.Equals(claim.OwnerDomain,
                    request.OwnerDomain, StringComparison.Ordinal)
                && string.Equals(claim.OwnerFacilityId,
                    request.OwnerDestinationId, StringComparison.Ordinal),
            _ => false
        };
        if (!ownerMatches)
        {
            failureReason = "owned-item-release-owner-mismatch";
            return false;
        }
        if (!gridSystemProvider.TryGetGrid(out Grid grid)
            || grid == null
            || !grid.TryFindNearbyWalkablePositionOnSameFloor(
                source.position,
                out Vector2Int outboundPosition,
                maxDistance: 1))
        {
            failureReason = "owned-item-release-outbound-cell-unavailable";
            return false;
        }

        if (!physicalItemRelocations.TryRelocateQuantity(
                source.stackId,
                source.quantity,
                outboundPosition,
                WorldItemStackState.Loose,
                string.Empty,
                request.OperationId,
                request.ReasonCode,
                out receipt,
                out failureReason))
        {
            return false;
        }
        if (!receipt.IsCommitted
            || !string.Equals(receipt.SourceStackId,
                request.StackId, StringComparison.Ordinal)
            || !string.Equals(receipt.DestinationStackId,
                request.StackId, StringComparison.Ordinal)
            || receipt.Quantity != request.Quantity)
        {
            throw new InvalidOperationException(
                "Exact owned-item release committed without preserving its whole-stack identity.");
        }
        return true;
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsCanonicalOptional(string value) =>
        value != null
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
