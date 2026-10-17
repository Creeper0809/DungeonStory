using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class WorldItemQuantityTransferService : IReservedItemTransferService
{
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IDungeonDebugRuleQuery debugRules;
    private readonly WorldItemQueryService itemQueries;
    private readonly IItemMarkerPresenter markerPresenter;
    private readonly WorldItemTransferPhysicalProjection physicalProjection;
    private readonly IItemQuantityLeaseMutation quantityLeaseMutations;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly WorldItemRepository repository;
    private long facilityConsumptionSequence;
    private long directConsumptionSequence;

    internal WorldItemQuantityTransferService(
        IDungeonItemCatalogProvider catalogProvider,
        IDungeonDebugRuleQuery debugRules,
        WorldItemQueryService itemQueries,
        IItemMarkerPresenter markerPresenter,
        WorldItemTransferPhysicalProjection physicalProjection,
        IItemQuantityLeaseMutation quantityLeaseMutations,
        IItemQuantityReservationService quantityReservations,
        WorldItemRepository repository)
    {
        this.catalogProvider = catalogProvider;
        this.debugRules = debugRules;
        this.itemQueries = itemQueries;
        this.markerPresenter = markerPresenter;
        this.physicalProjection = physicalProjection;
        this.quantityLeaseMutations = quantityLeaseMutations;
        this.quantityReservations = quantityReservations;
        this.repository = repository;
    }

    public int ReleaseQuantityReservationsByOwner(
        string ownerOperationId,
        ItemReservationReleaseReason reason) =>
        quantityReservations.ReleaseByOwner(ownerOperationId, reason);

    public bool RenewQuantityReservation(
        string leaseId,
        double requestedUntilGameSeconds,
        out DomainFailure failure) =>
        quantityReservations.Renew(
            leaseId,
            requestedUntilGameSeconds,
            out failure);

    public bool TryReserveAvailableStackForDirectPickup(
        string ownerCharacterId,
        string ownerOperationId,
        ItemReservationPurpose purpose,
        string stackId,
        int quantity,
        out ItemQuantityLease lease,
        out DomainFailure failure)
    {
        lease = null;
        failure = DomainFailure.None;
        string characterId = ownerCharacterId?.Trim() ?? string.Empty;
        string operationId = ownerOperationId?.Trim() ?? string.Empty;
        string normalizedStackId = stackId?.Trim() ?? string.Empty;
        if (characterId.Length == 0
            || operationId.Length == 0
            || quantity <= 0
            || !repository.RecordsById.TryGetValue(
                normalizedStackId,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0
            || record.state is not (WorldItemStackState.Loose
                or WorldItemStackState.Stored)
            || record.forbidden
            || FacilityOutputExactRouteCustodyCodec.IsRouteBlocked(
                record.components))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferStackUnavailable,
                normalizedStackId);
            return false;
        }

        return quantityReservations.TryReserve(
            operationId,
            characterId,
            purpose,
            $"direct-pickup:{purpose}:{characterId}",
            new ItemQuantityReservationRequest(
                new ItemStackId(record.stackId),
                quantity,
                ItemReservationSignature.Create(record.itemId, record.components)),
            out lease,
            out failure);
    }

    public bool ReleaseQuantityReservation(
        string leaseId,
        ItemReservationReleaseReason reason) =>
        quantityReservations.Release(leaseId, reason);

    public bool TryExtractReservedQuantity(
        string leaseId,
        int quantity,
        ItemTransitDestination destination,
        out ItemExtractionReceipt receipt,
        out DomainFailure failure)
    {
        receipt = default;
        failure = DomainFailure.None;
        if (quantity <= 0
            || !quantityReservations.Revalidate(
                leaseId,
                out ItemQuantityLease lease,
                out failure)
            || lease.remainingQuantity < quantity)
        {
            if (!failure.IsFailure)
                failure = new DomainFailure(
                    FailureCode.ItemReservationQuantityUnavailable,
                    leaseId ?? string.Empty);
            return false;
        }

        ItemLeaseSlice sourceSlice = lease.slices.FirstOrDefault(slice =>
            slice != null
            && slice.quantity >= quantity
            && repository.RecordsById.ContainsKey(slice.stackId));
        if (sourceSlice == null
            || !repository.RecordsById.TryGetValue(
                sourceSlice.stackId,
                out WorldItemStackRecord source)
            || source == null
            || source.quantity < quantity)
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationSliceInvalid,
                leaseId ?? string.Empty,
                sourceSlice?.stackId ?? string.Empty);
            return false;
        }

        string sourceId = source.stackId;
        bool transferredIdentity = source.quantity == quantity
            && source.reservedQuantity == quantity;
        string extractedId = transferredIdentity
            ? source.stackId
            : repository.AllocateStackId();
        Vector2Int sourcePosition = source.position;
        string itemId = source.itemId;
        WorldItemStackState transitState = destination.State is
            WorldItemStackState.Carried or WorldItemStackState.InTransit
                ? destination.State
                : WorldItemStackState.InTransit;
        List<ItemInstanceComponentSaveData> originalComponents =
            WorldItemTransferMetadataRules.CloneComponents(source.components);
        if (!TryPrepareCustodyExtraction(
                source,
                quantity,
                out List<ItemInstanceComponentSaveData> sourceComponents,
                out List<ItemInstanceComponentSaveData> extractedComponents,
                out failure))
        {
            return false;
        }
        if (transferredIdentity)
        {
            repository.Relocate(source, destination.Position);
            source.state = transitState;
            source.destinationId = destination.DestinationId;
            source.aggregationCohortId = lease.aggregationCohortId;
            source.hasDestinationPosition = destination.DestinationId.Length > 0;
            source.destinationPosition = destination.Position;
            source.sourceStorageDestinationId = string.Empty;
            WorldItemTransferMetadataRules.ClearTransientRecoveryMetadata(source);
            repository.MarkChanged();
        }
        else
        {
            WorldItemStackRecord child = CloneForTransit(
                source,
                extractedId,
                quantity,
                transitState,
                destination,
                lease.aggregationCohortId,
                extractedComponents);
            source.quantity -= quantity;
            source.components = sourceComponents;
            repository.Add(child);

            string sourceSignature = ItemReservationSignature.Create(
                source.itemId,
                source.components);
            string extractedSignature = ItemReservationSignature.Create(
                child.itemId,
                child.components);

            List<ItemLeaseSlice> replacements = BuildExtractionReplacements(
                lease,
                sourceId,
                extractedId,
                sourceSignature,
                extractedSignature,
                quantity);
            if (replacements == null
                || !quantityLeaseMutations.TryRetargetSlices(
                    new Dictionary<string, IReadOnlyList<ItemLeaseSlice>>
                    {
                        [lease.leaseId] = replacements
                    },
                    out failure))
            {
                repository.Remove(child);
                source.quantity += quantity;
                source.components = originalComponents;
                repository.MarkChanged();
                if (!failure.IsFailure)
                {
                    failure = new DomainFailure(
                        FailureCode.ItemReservationSliceInvalid,
                        lease.leaseId,
                        sourceId);
                }
                return false;
            }
        }

        markerPresenter.RefreshAt(sourcePosition);
        markerPresenter.RefreshAt(destination.Position);
        receipt = new ItemExtractionReceipt(
            leaseId,
            sourceId,
            extractedId,
            itemId,
            quantity,
            transferredIdentity ? 0 : source.quantity,
            transferredIdentity);
        return true;
    }

    public bool TryConsumeReservedQuantity(
        string leaseId,
        int quantity,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (quantity <= 0
            || !quantityReservations.Revalidate(
                leaseId,
                out ItemQuantityLease lease,
                out failure)
            || lease.remainingQuantity < quantity)
        {
            if (!failure.IsFailure)
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationQuantityUnavailable,
                    leaseId ?? string.Empty);
            }
            return false;
        }

        List<(WorldItemStackRecord record, int quantity)> removals = new();
        int remaining = quantity;
        foreach (ItemLeaseSlice slice in lease.slices)
        {
            if (slice == null || slice.quantity <= 0 || remaining <= 0)
                continue;
            if (!repository.RecordsById.TryGetValue(
                    slice.stackId,
                    out WorldItemStackRecord record)
                || record == null)
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationSliceInvalid,
                    lease.leaseId,
                    slice.stackId);
                return false;
            }
            int take = Mathf.Min(remaining, slice.quantity);
            removals.Add((record, take));
            remaining -= take;
        }
        if (remaining > 0)
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationQuantityUnavailable,
                lease.leaseId);
            return false;
        }
        if (removals.Any(candidate =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    candidate.record.components)))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferConsumptionFailed,
                lease.leaseId,
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass
                    .ToString());
            return false;
        }
        if (!quantityLeaseMutations.TryConsumeSlices(
                lease.leaseId,
                quantity,
                out _,
                out failure))
        {
            return false;
        }
        HashSet<Vector2Int> touched = new();
        foreach ((WorldItemStackRecord record, int remove) in removals)
        {
            touched.Add(record.position);
            record.quantity -= remove;
            if (record.quantity <= 0)
            {
                if (!string.IsNullOrWhiteSpace(record.itemInstanceId))
                {
                    repository.TryMarkEquipmentLostBySourceStack(record.stackId);
                    repository.TryMarkModuleLostBySourceStack(record.stackId);
                }
                repository.Remove(record);
            }
        }
        repository.MarkChanged();
        foreach (Vector2Int position in touched)
            markerPresenter.RefreshAt(position);
        return true;
    }

    private static WorldItemStackRecord CloneForTransit(
        WorldItemStackRecord source,
        string stackId,
        int quantity,
        WorldItemStackState state,
        ItemTransitDestination destination,
        string aggregationCohortId,
        IReadOnlyList<ItemInstanceComponentSaveData> components) => new()
    {
        stackId = stackId,
        itemInstanceId = source.itemInstanceId,
        itemId = source.itemId,
        quantity = quantity,
        state = state,
        position = destination.Position,
        destinationId = destination.DestinationId,
        aggregationCohortId = aggregationCohortId?.Trim() ?? string.Empty,
        hasDestinationPosition = destination.DestinationId.Length > 0,
        destinationPosition = destination.Position,
        forbidden = source.forbidden,
        sourceCharacterId = source.sourceCharacterId,
        sourceDisplayName = source.sourceDisplayName,
        sourceSpeciesTag = source.sourceSpeciesTag,
        sourceDeathReason = source.sourceDeathReason,
        emergencyButcheryAllowed = source.emergencyButcheryAllowed,
        wasteOrigin = source.wasteOrigin,
        contamination = source.contamination,
        components = (components ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null)
            .Select(component => component.Clone())
            .ToList()
    };

    internal bool TryPrepareCustodyExtraction(
        WorldItemStackRecord source,
        int extractedQuantity,
        out List<ItemInstanceComponentSaveData> sourceComponents,
        out List<ItemInstanceComponentSaveData> extractedComponents,
        out DomainFailure failure)
    {
        sourceComponents = WorldItemTransferMetadataRules.CloneComponents(source?.components);
        extractedComponents = WorldItemTransferMetadataRules.CloneComponents(source?.components);
        failure = DomainFailure.None;
        if (source == null)
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationSliceInvalid,
                string.Empty,
                "physical-source-missing");
            return false;
        }
        if (!FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                source.components))
        {
            return true;
        }

        if (!FacilityOutputExactRouteCustodyCodec.TryRead(
                source.components,
                out FacilityOutputExactRouteCustodyMetadata custody)
            || custody.Phase != FacilityOutputExactRouteCustodyPhase.Routable
            || custody.Quantity != source.quantity
            || !string.Equals(custody.ItemId, source.itemId,
                StringComparison.Ordinal)
            || (source.state is not (WorldItemStackState.Carried
                    or WorldItemStackState.InTransit)
                && !string.Equals(
                    custody.CurrentTargetDestinationId,
                    source.destinationId,
                    StringComparison.Ordinal))
            || extractedQuantity <= 0
            || extractedQuantity > source.quantity)
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationSliceInvalid,
                source?.stackId ?? string.Empty,
                "malformed-or-blocked-exact-route-custody");
            return false;
        }

        List<ItemInstanceComponentSaveData> business =
            WorldItemTransferMetadataRules.CapturePhysicalBusinessComponents(source.components);
        string signature = FacilityBufferPlannedOutputPublicationService
            .CreateRuntimeComponentSignature(business);
        long totalMass = physicalProjection.GetPhysicalQuantityMass(
            source.itemId,
            source.itemInstanceId,
            business,
            source.quantity);
        if (!string.Equals(
                signature,
                custody.ComponentSignature,
                StringComparison.Ordinal)
            || totalMass != custody.MassGrams)
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationSliceInvalid,
                source.stackId,
                "exact-route-custody-payload-changed");
            return false;
        }

        if (extractedQuantity == source.quantity)
            return true;

        int remainderQuantity = checked(source.quantity - extractedQuantity);
        long extractedMass = physicalProjection.GetPhysicalQuantityMass(
            source.itemId,
            source.itemInstanceId,
            business,
            extractedQuantity);
        long remainderMass = physicalProjection.GetPhysicalQuantityMass(
            source.itemId,
            source.itemInstanceId,
            business,
            remainderQuantity);
        if (!custody.TryPartitionRoutablePrefix(
                source.stackId,
                extractedQuantity,
                extractedMass,
                remainderMass,
                out FacilityOutputExactRouteCustodyMetadata extracted,
                out FacilityOutputExactRouteCustodyMetadata remainder))
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationSliceInvalid,
                source.stackId,
                "exact-route-custody-partition-invalid");
            return false;
        }

        sourceComponents = FacilityOutputExactRouteCustodyCodec
            .ReplaceAuthority(source.components, remainder);
        extractedComponents = FacilityOutputExactRouteCustodyCodec
            .ReplaceAuthority(source.components, extracted);
        return true;
    }

    private static List<ItemLeaseSlice> BuildExtractionReplacements(
        ItemQuantityLease lease,
        string sourceStackId,
        string extractedStackId,
        string sourceExpectedSignature,
        string extractedExpectedSignature,
        int quantity)
    {
        int remaining = quantity;
        List<ItemLeaseSlice> replacements = new();
        foreach (ItemLeaseSlice slice in lease.slices)
        {
            if (slice == null || slice.quantity <= 0)
                continue;
            if (!string.Equals(
                    slice.stackId,
                    sourceStackId,
                    StringComparison.Ordinal)
                || remaining <= 0)
            {
                replacements.Add(slice.Clone());
                continue;
            }
            int moved = Mathf.Min(remaining, slice.quantity);
            int left = slice.quantity - moved;
            if (left > 0)
            {
                replacements.Add(new ItemLeaseSlice
                {
                    stackId = slice.stackId,
                    originStackId = string.IsNullOrWhiteSpace(slice.originStackId)
                        ? slice.stackId
                        : slice.originStackId,
                    expectedStackSignature = sourceExpectedSignature,
                    quantity = left
                });
            }
            if (moved > 0)
            {
                replacements.Add(new ItemLeaseSlice
                {
                    stackId = extractedStackId,
                    originStackId = string.IsNullOrWhiteSpace(slice.originStackId)
                        ? slice.stackId
                        : slice.originStackId,
                    expectedStackSignature = extractedExpectedSignature,
                    quantity = moved
                });
            }
            remaining -= moved;
        }
        if (remaining > 0)
            return null;
        return replacements;
    }

    public bool TryConsumeStackQuantity(
        ItemStackId stackId,
        int quantity,
        out WorldItemStackSnapshot consumed,
        out DomainFailure failure)
    {
        consumed = null;
        failure = DomainFailure.None;
        if (!stackId.IsValid
            || quantity <= 0
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0
            || record.quantity - record.reservedQuantity < quantity)
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferStackUnavailable,
                stackId.Value);
            return false;
        }

        if (FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                record.components))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferConsumptionFailed,
                stackId.Value,
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass
                    .ToString());
            return false;
        }

        WorldItemStackSnapshot snapshot = itemQueries.CreateSnapshot(record);
        snapshot.Quantity = quantity;
        if (debugRules.ShouldSkipCosts())
        {
            consumed = snapshot;
            return consumed.Quantity > 0;
        }

        string operationId =
            $"direct-item-consume:{++directConsumptionSequence:D16}:{stackId.Value}";
        string signature = ItemReservationSignature.Create(
            record.itemId,
            record.components);
        if (!quantityReservations.TryReserve(
                operationId,
                string.Empty,
                ItemReservationPurpose.DirectPlayerOrder,
                $"direct-consume:{record.itemId}",
                new ItemQuantityReservationRequest(
                    stackId,
                    quantity,
                    signature),
                out ItemQuantityLease lease,
                out failure))
        {
            return false;
        }
        if (!TryConsumeReservedQuantity(
                lease.leaseId,
                quantity,
                out failure))
        {
            quantityReservations.ReleaseByOwner(
                operationId,
                ItemReservationReleaseReason.Cancelled);
            return false;
        }
        consumed = snapshot;
        return true;
    }

    public bool TryConsumeFacilityBuffer(
        string destinationId,
        IReadOnlyDictionary<StockCategory, int> costs,
        out string failureReason) =>
        ItemFacilityBufferTransaction.TryConsumeByCategory(
            destinationId,
            costs,
            debugRules,
            repository,
            catalogProvider,
            markerPresenter,
            quantityReservations,
            this,
            AllocateFacilityConsumptionOperationId(destinationId),
            out failureReason);

    public bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason) =>
        ItemFacilityBufferTransaction.TryConsumeByItem(
            destinationId,
            costs,
            debugRules,
            repository,
            markerPresenter,
            quantityReservations,
            this,
            AllocateFacilityConsumptionOperationId(destinationId),
            out failureReason);

    private string AllocateFacilityConsumptionOperationId(string destinationId) =>
        $"facility-consume:{destinationId?.Trim() ?? string.Empty}:{++facilityConsumptionSequence}";
}
