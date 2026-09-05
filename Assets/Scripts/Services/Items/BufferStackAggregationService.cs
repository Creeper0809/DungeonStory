using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public readonly struct BufferAggregationKey : IEquatable<BufferAggregationKey>
{
    public BufferAggregationKey(
        string destinationId,
        string aggregationCohortId,
        string itemId,
        string nonFreshnessSignature,
        int freshnessBucket,
        bool preserved,
        int contaminationBucket)
    {
        DestinationId = destinationId?.Trim() ?? string.Empty;
        AggregationCohortId = aggregationCohortId?.Trim() ?? string.Empty;
        ItemId = itemId?.Trim() ?? string.Empty;
        NonFreshnessSignature = nonFreshnessSignature?.Trim() ?? string.Empty;
        FreshnessBucket = freshnessBucket;
        Preserved = preserved;
        ContaminationBucket = contaminationBucket;
    }

    public string DestinationId { get; }
    public string AggregationCohortId { get; }
    public string ItemId { get; }
    public string NonFreshnessSignature { get; }
    public int FreshnessBucket { get; }
    public bool Preserved { get; }
    public int ContaminationBucket { get; }

    public bool Equals(BufferAggregationKey other) =>
        FreshnessBucket == other.FreshnessBucket
        && Preserved == other.Preserved
        && ContaminationBucket == other.ContaminationBucket
        && string.Equals(DestinationId, other.DestinationId, StringComparison.Ordinal)
        && string.Equals(AggregationCohortId, other.AggregationCohortId, StringComparison.Ordinal)
        && string.Equals(ItemId, other.ItemId, StringComparison.Ordinal)
        && string.Equals(
            NonFreshnessSignature,
            other.NonFreshnessSignature,
            StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is BufferAggregationKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        DestinationId,
        AggregationCohortId,
        ItemId,
        NonFreshnessSignature,
        FreshnessBucket,
        Preserved,
        ContaminationBucket);
}

public readonly struct BufferAggregationReceipt
{
    public BufferAggregationReceipt(
        string incomingStackId,
        string canonicalStackId,
        int depositedQuantity,
        int canonicalQuantity,
        int removedPhysicalStackCount)
    {
        IncomingStackId = incomingStackId?.Trim() ?? string.Empty;
        CanonicalStackId = canonicalStackId?.Trim() ?? string.Empty;
        DepositedQuantity = Mathf.Max(0, depositedQuantity);
        CanonicalQuantity = Mathf.Max(0, canonicalQuantity);
        RemovedPhysicalStackCount = Mathf.Max(0, removedPhysicalStackCount);
    }

    public string IncomingStackId { get; }
    public string CanonicalStackId { get; }
    public int DepositedQuantity { get; }
    public int CanonicalQuantity { get; }
    public int RemovedPhysicalStackCount { get; }
}

public interface IBufferStackAggregationService
{
    bool TryDepositAndAggregate(
        CharacterCarriedItemSaveData item,
        ItemReservationPurpose purpose,
        string aggregationCohortId,
        string destinationId,
        Vector2Int destinationPosition,
        out BufferAggregationReceipt receipt,
        out DomainFailure failure);

    int PendingAggregationCount { get; }

    int ProcessPending(
        int maxOperations = 64,
        bool beginNewTick = false);
}

/// <summary>
/// Event-driven facility-buffer aggregation. Reservations never create physical
/// stacks; transport children are repacked only after reaching a compatible buffer.
/// </summary>
public sealed class BufferStackAggregationService :
    IBufferStackAggregationService,
    ITickable
{
    private const float FreshnessBucketSeconds = 2f;
    private const float ContaminationTolerance = 0.01f;
    private const int MaximumAggregationsPerTick = 64;

    private readonly IDungeonItemCatalogProvider catalog;
    private readonly WorldItemRepository repository;
    private readonly IItemMarkerPresenter markers;
    private readonly IItemQuantityReservationService reservations;
    private readonly IItemQuantityLeaseMutation leaseMutations;
    private readonly Dictionary<BufferAggregationKey, List<string>> targetsByKey =
        new();
    private readonly Queue<PendingAggregationRequest> pending = new();
    private readonly HashSet<string> pendingStackIds = new(StringComparer.Ordinal);
    private int aggregationOperationsThisTick;

    public BufferStackAggregationService(
        IDungeonItemCatalogProvider catalog,
        WorldItemRepository repository,
        IItemMarkerPresenter markers,
        IItemQuantityReservationService reservations,
        IItemQuantityLeaseMutation leaseMutations)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.markers = markers ?? throw new ArgumentNullException(nameof(markers));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.leaseMutations = leaseMutations
            ?? throw new ArgumentNullException(nameof(leaseMutations));
    }

    public int PendingAggregationCount => pending.Count;

    public void Tick()
    {
        ProcessPending(MaximumAggregationsPerTick, beginNewTick: true);
    }

    public int ProcessPending(
        int maxOperations = MaximumAggregationsPerTick,
        bool beginNewTick = false)
    {
        if (beginNewTick)
            aggregationOperationsThisTick = 0;
        int budget = Mathf.Min(
            Mathf.Max(0, maxOperations),
            MaximumAggregationsPerTick - aggregationOperationsThisTick);
        int processed = 0;
        while (processed < budget && pending.Count > 0)
        {
            PendingAggregationRequest request = pending.Dequeue();
            pendingStackIds.Remove(request.StackId);
            if (!repository.RecordsById.TryGetValue(
                    request.StackId,
                    out WorldItemStackRecord record)
                || record == null
                || record.quantity <= 0
                || record.state != WorldItemStackState.FacilityBuffer)
            {
                continue;
            }

            RequireNoPreparedOutputCustody(
                record.components,
                nameof(ProcessPending));

            CharacterCarriedItemSaveData item = new()
            {
                carriedStackId = record.stackId,
                sourceStackId = request.SourceStackId,
                ownerOperationId = request.OwnerOperationId,
                itemInstanceId = record.itemInstanceId,
                itemId = record.itemId,
                quantity = record.quantity,
                wasteOrigin = record.wasteOrigin,
                contamination = record.contamination,
                components = CloneComponents(record.components)
            };
            TryDepositAndAggregateCore(
                item,
                request.Purpose,
                request.AggregationCohortId,
                request.DestinationId,
                request.DestinationPosition,
                out _,
                out _);
            aggregationOperationsThisTick++;
            processed++;
        }
        return processed;
    }

    public bool TryDepositAndAggregate(
        CharacterCarriedItemSaveData item,
        ItemReservationPurpose purpose,
        string aggregationCohortId,
        string destinationId,
        Vector2Int destinationPosition,
        out BufferAggregationReceipt receipt,
        out DomainFailure failure)
    {
        RequireNoPreparedOutputCustody(
            item?.components,
            nameof(TryDepositAndAggregate));
        RequireRepositoryStackHasNoPreparedOutputCustody(
            item?.carriedStackId,
            nameof(TryDepositAndAggregate));

        if (aggregationOperationsThisTick >= MaximumAggregationsPerTick)
        {
            return TryStagePendingAggregation(
                item,
                purpose,
                aggregationCohortId,
                destinationId,
                destinationPosition,
                out receipt,
                out failure);
        }

        aggregationOperationsThisTick++;
        return TryDepositAndAggregateCore(
            item,
            purpose,
            aggregationCohortId,
            destinationId,
            destinationPosition,
            out receipt,
            out failure);
    }

    private bool TryStagePendingAggregation(
        CharacterCarriedItemSaveData item,
        ItemReservationPurpose purpose,
        string aggregationCohortId,
        string destinationId,
        Vector2Int destinationPosition,
        out BufferAggregationReceipt receipt,
        out DomainFailure failure)
    {
        receipt = default;
        failure = DomainFailure.None;
        RequireNoPreparedOutputCustody(
            item?.components,
            nameof(TryStagePendingAggregation));
        string stackId = item?.carriedStackId?.Trim() ?? string.Empty;
        if (item == null
            || item.quantity <= 0
            || purpose is not (ItemReservationPurpose.Meal
                or ItemReservationPurpose.ProductionInput)
            || string.IsNullOrWhiteSpace(aggregationCohortId)
            || string.IsNullOrWhiteSpace(destinationId)
            || stackId.Length == 0)
        {
            failure = new DomainFailure(
                FailureCode.ItemAggregationIncompatible,
                item?.itemId ?? string.Empty);
            return false;
        }

        WorldItemStackRecord transport;
        if (!repository.RecordsById.TryGetValue(stackId, out transport))
        {
            DungeonItemDefinition definition = catalog.GetDefinition(item.itemId);
            if (!string.IsNullOrWhiteSpace(item.itemInstanceId)
                || definition.MaxStack <= 1
                || item.quantity > definition.MaxStack)
            {
                failure = new DomainFailure(
                    FailureCode.ItemAggregationIncompatible,
                    item.itemId);
                return false;
            }
            transport = new WorldItemStackRecord
            {
                stackId = stackId,
                itemId = item.itemId,
                quantity = item.quantity,
                state = WorldItemStackState.FacilityBuffer,
                position = destinationPosition,
                wasteOrigin = item.wasteOrigin,
                contamination = Mathf.Clamp(item.contamination, 0f, 100f),
                components = CloneComponents(item.components)
            };
            repository.Add(transport);
        }
        else if (transport == null
            || transport.quantity != item.quantity
            || transport.state is not (WorldItemStackState.Carried
                or WorldItemStackState.InTransit
                or WorldItemStackState.FacilityBuffer))
        {
            failure = new DomainFailure(
                FailureCode.ItemAggregationIncompatible,
                item.itemId);
            return false;
        }

        RequireNoPreparedOutputCustody(
            transport.components,
            nameof(TryStagePendingAggregation));

        string normalizedDestination = destinationId.Trim();
        string normalizedCohort = aggregationCohortId.Trim();
        repository.Relocate(transport, destinationPosition);
        transport.state = WorldItemStackState.FacilityBuffer;
        transport.destinationId = normalizedDestination;
        transport.aggregationCohortId = normalizedCohort;
        transport.hasDestinationPosition = true;
        transport.destinationPosition = destinationPosition;

        if (pendingStackIds.Add(transport.stackId))
        {
            pending.Enqueue(new PendingAggregationRequest(
                transport.stackId,
                item.sourceStackId,
                item.ownerOperationId,
                purpose,
                normalizedCohort,
                normalizedDestination,
                destinationPosition));
        }
        repository.MarkChanged();
        markers.RefreshAt(destinationPosition);
        receipt = new BufferAggregationReceipt(
            transport.stackId,
            transport.stackId,
            transport.quantity,
            transport.quantity,
            0);
        return true;
    }

    private bool TryDepositAndAggregateCore(
        CharacterCarriedItemSaveData item,
        ItemReservationPurpose purpose,
        string aggregationCohortId,
        string destinationId,
        Vector2Int destinationPosition,
        out BufferAggregationReceipt receipt,
        out DomainFailure failure)
    {
        receipt = default;
        failure = DomainFailure.None;
        RequireNoPreparedOutputCustody(
            item?.components,
            nameof(TryDepositAndAggregateCore));
        if (item == null
            || item.quantity <= 0
            || string.IsNullOrWhiteSpace(item.itemId)
            || purpose is not (ItemReservationPurpose.Meal
                or ItemReservationPurpose.ProductionInput)
            || string.IsNullOrWhiteSpace(destinationId)
            || string.IsNullOrWhiteSpace(aggregationCohortId))
        {
            failure = new DomainFailure(
                FailureCode.ItemAggregationIncompatible,
                item?.itemId ?? string.Empty);
            return false;
        }

        DungeonItemDefinition definition = catalog.GetDefinition(item.itemId);
        if (!string.IsNullOrWhiteSpace(item.itemInstanceId)
            || definition.MaxStack <= 1)
        {
            failure = new DomainFailure(
                FailureCode.ItemAggregationIncompatible,
                item.itemId);
            return false;
        }

        WorldItemStackRecord transport = null;
        string carriedStackId = item.carriedStackId?.Trim() ?? string.Empty;
        if (carriedStackId.Length > 0
            && repository.RecordsById.TryGetValue(
                carriedStackId,
                out WorldItemStackRecord resolvedTransport))
        {
            if (resolvedTransport == null
                || resolvedTransport.quantity != item.quantity
                || resolvedTransport.state is not (WorldItemStackState.Carried
                    or WorldItemStackState.InTransit
                    or WorldItemStackState.FacilityBuffer)
                || !string.Equals(
                    resolvedTransport.itemId,
                    item.itemId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    ItemStackSignature.Create(
                        resolvedTransport.itemId,
                        resolvedTransport.components),
                    item.GetStackSignature(),
                    StringComparison.Ordinal))
            {
                failure = new DomainFailure(
                    FailureCode.ItemAggregationIncompatible,
                    item.itemId);
                return false;
            }
            RequireNoPreparedOutputCustody(
                resolvedTransport.components,
                nameof(TryDepositAndAggregateCore));
            transport = resolvedTransport;
        }

        FreshnessProjection freshness = ReadFreshness(item.components);
        Vector2Int transportOriginalPosition = transport?.position ?? default;
        WorldItemStackState transportOriginalState = transport?.state
            ?? WorldItemStackState.Carried;
        string transportOriginalDestination = transport?.destinationId ?? string.Empty;
        string transportOriginalCohort = transport?.aggregationCohortId ?? string.Empty;
        bool transportOriginalHasDestination = transport?.hasDestinationPosition ?? false;
        Vector2Int transportOriginalDestinationPosition =
            transport?.destinationPosition ?? default;
        List<ItemInstanceComponentSaveData> transportOriginalComponents =
            CloneComponents(transport?.components);
        BufferAggregationKey key = new(
            destinationId,
            aggregationCohortId,
            item.itemId,
            CreateNonFreshnessSignature(item.itemId, item.components),
            freshness.HasValue
                ? Mathf.FloorToInt(freshness.RemainingSeconds / FreshnessBucketSeconds)
                : -1,
            freshness.Preserved,
            Mathf.RoundToInt(item.contamination / ContaminationTolerance));
        bool preservesOutputProvenance =
            (item.components ?? new List<ItemInstanceComponentSaveData>())
            .Any(PlannedOutputPublicationComponentCodec.IsAnyMarker);
        List<WorldItemStackRecord> compatible = preservesOutputProvenance
            ? new List<WorldItemStackRecord>()
            : ResolveTargets(
                key,
                destinationPosition,
                item.contamination,
                freshness,
                transport?.stackId);

        int remaining = item.quantity;
        string canonicalId = string.Empty;
        int canonicalQuantity = 0;
        List<(WorldItemStackRecord record, int moved)> allocations = new();
        Dictionary<string, int> originalQuantities = new(StringComparer.Ordinal);
        Dictionary<string, List<ItemInstanceComponentSaveData>> originalComponents =
            new(StringComparer.Ordinal);
        for (int index = 0; index < compatible.Count && remaining > 0; index++)
        {
            WorldItemStackRecord target = compatible[index];
            int capacity = Mathf.Max(0, definition.MaxStack - target.quantity);
            int moved = Mathf.Min(capacity, remaining);
            if (moved <= 0)
                continue;
            originalQuantities[target.stackId] = target.quantity;
            originalComponents[target.stackId] = CloneComponents(target.components);
            target.quantity += moved;
            remaining -= moved;
            ApplyConservativeFreshness(target.components, freshness);
            allocations.Add((target, moved));
            canonicalId = target.stackId;
            canonicalQuantity = target.quantity;
        }

        int createdCount = 0;
        if (transport != null && remaining > 0)
        {
            repository.Relocate(transport, destinationPosition);
            transport.quantity = remaining;
            transport.state = WorldItemStackState.FacilityBuffer;
            transport.destinationId = destinationId.Trim();
            transport.aggregationCohortId = aggregationCohortId.Trim();
            transport.hasDestinationPosition = true;
            transport.destinationPosition = destinationPosition;
            ApplyConservativeFreshness(transport.components, freshness);
            allocations.Add((transport, remaining));
            compatible.Add(transport);
            AddTarget(key, transport.stackId);
            canonicalId = transport.stackId;
            canonicalQuantity = transport.quantity;
            remaining = 0;
        }
        while (remaining > 0)
        {
            int packed = Mathf.Min(definition.MaxStack, remaining);
            WorldItemStackRecord created = new()
            {
                stackId = createdCount == 0
                    && !string.IsNullOrWhiteSpace(item.carriedStackId)
                    && !repository.RecordsById.ContainsKey(item.carriedStackId)
                        ? item.carriedStackId
                        : repository.AllocateStackId(),
                itemId = item.itemId,
                quantity = packed,
                state = WorldItemStackState.FacilityBuffer,
                position = destinationPosition,
                destinationId = destinationId.Trim(),
                aggregationCohortId = aggregationCohortId.Trim(),
                hasDestinationPosition = true,
                destinationPosition = destinationPosition,
                wasteOrigin = item.wasteOrigin,
                contamination = Mathf.Clamp(item.contamination, 0f, 100f),
                components = (item.components
                        ?? new List<ItemInstanceComponentSaveData>())
                    .Where(component => component != null)
                    .Select(component => component.Clone())
                    .ToList()
            };
            repository.Add(created);
            compatible.Add(created);
            AddTarget(key, created.stackId);
            allocations.Add((created, packed));
            remaining -= packed;
            canonicalId = created.stackId;
            canonicalQuantity = created.quantity;
            createdCount++;
        }

        if (transport != null)
        {
            if (!TryRetargetTransportReservations(
                    item.ownerOperationId,
                    transport.stackId,
                    allocations,
                    out failure))
            {
                RollBackAggregation(
                    transport,
                    item,
                    originalQuantities,
                    originalComponents,
                    allocations,
                    transportOriginalPosition,
                    transportOriginalState,
                    transportOriginalDestination,
                    transportOriginalCohort,
                    transportOriginalHasDestination,
                    transportOriginalDestinationPosition,
                    transportOriginalComponents);
                return false;
            }
            if (allocations.All(value => !ReferenceEquals(value.record, transport)))
                repository.Remove(transport);
        }

        repository.MarkChanged();
        markers.RefreshAt(destinationPosition);
        receipt = new BufferAggregationReceipt(
            item.carriedStackId,
            canonicalId,
            item.quantity,
            canonicalQuantity,
            transport != null
                && allocations.All(value => !ReferenceEquals(value.record, transport))
                    ? 1
                    : 0);
        return true;
    }

    private bool TryRetargetTransportReservations(
        string ownerOperationId,
        string sourceStackId,
        IReadOnlyList<(WorldItemStackRecord record, int moved)> allocations,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!reservations.TryGetLeasesByOwner(
                ownerOperationId,
                out IReadOnlyList<ItemQuantityLease> ownerLeases))
        {
            bool unreservedValid = repository.RecordsById.TryGetValue(
                    sourceStackId,
                    out WorldItemStackRecord unreserved)
                && unreserved.reservedQuantity == 0;
            if (!unreservedValid)
            {
                failure = new DomainFailure(
                    FailureCode.ItemReservationLeaseMissing,
                    ownerOperationId ?? string.Empty);
            }
            return unreservedValid;
        }

        List<ItemQuantityLease> affected = ownerLeases
            .Where(lease => lease?.slices?.Any(slice => slice != null
                && string.Equals(
                    slice.stackId,
                    sourceStackId,
                    StringComparison.Ordinal)) == true)
            .OrderBy(lease => lease.leaseId, StringComparer.Ordinal)
            .ToList();
        int affectedQuantity = affected.Sum(lease => lease.slices
            .Where(slice => slice != null
                && string.Equals(
                    slice.stackId,
                    sourceStackId,
                    StringComparison.Ordinal))
            .Sum(slice => slice.quantity));
        if (!repository.RecordsById.TryGetValue(
                sourceStackId,
                out WorldItemStackRecord source)
            || affectedQuantity != source.reservedQuantity)
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationSliceInvalid,
                ownerOperationId ?? string.Empty,
                sourceStackId);
            return false;
        }
        if (affectedQuantity == 0)
            return true;

        List<(WorldItemStackRecord record, int remaining)> capacity = allocations
            .Where(value => value.record != null && value.moved > 0)
            .Select(value => (value.record, value.moved))
            .ToList();
        int capacityIndex = 0;
        Dictionary<string, IReadOnlyList<ItemLeaseSlice>> replacements =
            new(StringComparer.Ordinal);
        foreach (ItemQuantityLease lease in affected)
        {
            ItemLeaseSlice[] movingSlices = lease.slices
                .Where(slice => slice != null
                    && string.Equals(
                        slice.stackId,
                        sourceStackId,
                        StringComparison.Ordinal))
                .ToArray();
            List<ItemLeaseSlice> next = lease.slices
                .Where(slice => slice != null
                    && !string.Equals(
                        slice.stackId,
                        sourceStackId,
                        StringComparison.Ordinal))
                .Select(slice => slice.Clone())
                .ToList();
            foreach (ItemLeaseSlice movingSlice in movingSlices)
            {
                int move = movingSlice.quantity;
                while (move > 0 && capacityIndex < capacity.Count)
                {
                    (WorldItemStackRecord target, int available) = capacity[capacityIndex];
                    int assigned = Mathf.Min(move, available);
                    next.Add(new ItemLeaseSlice
                    {
                        stackId = target.stackId,
                        originStackId = string.IsNullOrWhiteSpace(movingSlice.originStackId)
                            ? movingSlice.stackId
                            : movingSlice.originStackId,
                        expectedStackSignature = ItemReservationSignature.Create(
                            target.itemId,
                            target.components),
                        quantity = assigned
                    });
                    move -= assigned;
                    available -= assigned;
                    capacity[capacityIndex] = (target, available);
                    if (available == 0)
                        capacityIndex++;
                }
                if (move > 0)
                {
                    failure = new DomainFailure(
                        FailureCode.ItemReservationQuantityUnavailable,
                        sourceStackId);
                    return false;
                }
            }
            replacements.Add(lease.leaseId, next);
        }
        return leaseMutations.TryRetargetSlices(replacements, out failure);
    }

    private void RollBackAggregation(
        WorldItemStackRecord transport,
        CharacterCarriedItemSaveData item,
        IReadOnlyDictionary<string, int> originalQuantities,
        IReadOnlyDictionary<string, List<ItemInstanceComponentSaveData>> originalComponents,
        IReadOnlyList<(WorldItemStackRecord record, int moved)> allocations,
        Vector2Int originalPosition,
        WorldItemStackState originalState,
        string originalDestination,
        string originalCohort,
        bool originalHasDestination,
        Vector2Int originalDestinationPosition,
        IReadOnlyList<ItemInstanceComponentSaveData> originalTransportComponents)
    {
        foreach ((WorldItemStackRecord record, _) in allocations)
        {
            if (record == null || ReferenceEquals(record, transport))
                continue;
            if (originalQuantities.TryGetValue(record.stackId, out int original))
            {
                record.quantity = original;
                record.components = CloneComponents(originalComponents[record.stackId]);
            }
            else if (repository.RecordsById.ContainsKey(record.stackId))
            {
                repository.Remove(record);
            }
        }
        repository.Relocate(transport, originalPosition);
        transport.quantity = item.quantity;
        transport.state = originalState;
        transport.destinationId = originalDestination;
        transport.aggregationCohortId = originalCohort;
        transport.hasDestinationPosition = originalHasDestination;
        transport.destinationPosition = originalDestinationPosition;
        transport.components = CloneComponents(originalTransportComponents);
        repository.MarkChanged();
    }

    private static List<ItemInstanceComponentSaveData> CloneComponents(
        IReadOnlyList<ItemInstanceComponentSaveData> components) =>
        (components ?? Array.Empty<ItemInstanceComponentSaveData>())
        .Where(component => component != null)
        .Select(component => component.Clone())
        .ToList();

    private List<WorldItemStackRecord> ResolveTargets(
        BufferAggregationKey key,
        Vector2Int position,
        float contamination,
        FreshnessProjection incomingFreshness,
        string excludedStackId)
    {
        if (!targetsByKey.TryGetValue(key, out List<string> ids))
        {
            ids = new List<string>();
            targetsByKey.Add(key, ids);
            if (repository.RecordsByPosition.TryGetValue(
                    position,
                    out List<WorldItemStackRecord> local))
            {
                for (int index = 0; index < local.Count; index++)
                {
                    WorldItemStackRecord candidate = local[index];
                    if (IsCompatible(
                            candidate,
                            key,
                            contamination,
                            incomingFreshness))
                    {
                        ids.Add(candidate.stackId);
                    }
                }
            }
        }

        List<WorldItemStackRecord> resolved = new(ids.Count);
        for (int index = ids.Count - 1; index >= 0; index--)
        {
            if (!repository.RecordsById.TryGetValue(
                    ids[index],
                    out WorldItemStackRecord record)
                || string.Equals(
                    record?.stackId,
                    excludedStackId,
                    StringComparison.Ordinal)
                || !IsCompatible(
                    record,
                    key,
                    contamination,
                    incomingFreshness))
            {
                ids.RemoveAt(index);
                continue;
            }
            resolved.Add(record);
        }
        resolved.Sort((left, right) =>
        {
            int quantity = right.quantity.CompareTo(left.quantity);
            return quantity != 0
                ? quantity
                : string.CompareOrdinal(left.stackId, right.stackId);
        });
        return resolved;
    }

    private static bool IsCompatible(
        WorldItemStackRecord record,
        BufferAggregationKey key,
        float contamination,
        FreshnessProjection incomingFreshness)
    {
        if (record == null
            || record.quantity <= 0
            || record.state != WorldItemStackState.FacilityBuffer
            || FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                record.components)
            || (record.components
                    ?? new List<ItemInstanceComponentSaveData>())
                .Any(PlannedOutputPublicationComponentCodec.IsAnyMarker)
            || !string.Equals(record.destinationId, key.DestinationId, StringComparison.Ordinal)
            || !string.Equals(
                record.aggregationCohortId,
                key.AggregationCohortId,
                StringComparison.Ordinal)
            || !string.Equals(record.itemId, key.ItemId, StringComparison.Ordinal)
            || Mathf.Abs(record.contamination - contamination) > ContaminationTolerance
            || !string.Equals(
                CreateNonFreshnessSignature(record.itemId, record.components),
                key.NonFreshnessSignature,
                StringComparison.Ordinal))
        {
            return false;
        }
        FreshnessProjection current = ReadFreshness(record.components);
        if (current.HasValue != incomingFreshness.HasValue
            || current.Preserved != incomingFreshness.Preserved)
        {
            return false;
        }
        return !current.HasValue
            || Mathf.FloorToInt(current.RemainingSeconds / FreshnessBucketSeconds)
                == key.FreshnessBucket;
    }

    private void RequireRepositoryStackHasNoPreparedOutputCustody(
        string stackId,
        string operation)
    {
        string canonical = stackId?.Trim() ?? string.Empty;
        if (canonical.Length > 0
            && repository.RecordsById.TryGetValue(
                canonical,
                out WorldItemStackRecord record))
        {
            RequireNoPreparedOutputCustody(record?.components, operation);
        }
    }

    private static void RequireNoPreparedOutputCustody(
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        string operation)
    {
        if (!FacilityOutputExactRouteCustodyCodec.HasAnyCustody(components))
            return;

        // The live facility-deposit caller falls back to generic physical spawn
        // when aggregation returns false. Throwing the typed bypass exception is
        // therefore required to stop both the merge and that fallback boundary.
        throw new FacilityOutputExactRouteBypassException(
            FacilityOutputExactRouteFailureCode.ProtectedRouteBypass,
            operation);
    }

    private void AddTarget(BufferAggregationKey key, string stackId)
    {
        if (!targetsByKey.TryGetValue(key, out List<string> ids))
        {
            ids = new List<string>();
            targetsByKey.Add(key, ids);
        }
        if (!ids.Contains(stackId))
            ids.Add(stackId);
    }

    private static string CreateNonFreshnessSignature(
        string itemId,
        IReadOnlyList<ItemInstanceComponentSaveData> components) =>
        ItemStackSignature.Create(
            itemId,
            (components ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null
                && !string.Equals(
                    component.componentTypeId,
                    ItemInstanceComponentIds.Freshness,
                    StringComparison.Ordinal)));

    private static FreshnessProjection ReadFreshness(
        IReadOnlyList<ItemInstanceComponentSaveData> components)
    {
        ItemInstanceComponentSaveData component = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .FirstOrDefault(value => value != null
                && string.Equals(
                    value.componentTypeId,
                    ItemInstanceComponentIds.Freshness,
                    StringComparison.Ordinal));
        if (component == null)
            return default;
        double remaining = ReadDecimal(component, "remaining-seconds", 0d);
        bool preserved = ReadBoolean(component, "preserved", false);
        return new FreshnessProjection(true, Mathf.Max(0f, (float)remaining), preserved);
    }

    private static void ApplyConservativeFreshness(
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        FreshnessProjection incoming)
    {
        if (!incoming.HasValue || components == null)
            return;
        ItemInstanceComponentSaveData component = components.FirstOrDefault(value =>
            value != null
            && string.Equals(
                value.componentTypeId,
                ItemInstanceComponentIds.Freshness,
                StringComparison.Ordinal));
        ItemStateValueSaveData remaining = component?.values?.FirstOrDefault(value =>
            value != null
            && string.Equals(value.key, "remaining-seconds", StringComparison.Ordinal));
        if (remaining != null)
        {
            remaining.kind = ItemStateValueKind.Decimal;
            remaining.decimalValue = Math.Min(
                remaining.decimalValue,
                incoming.RemainingSeconds);
        }
    }

    private static double ReadDecimal(
        ItemInstanceComponentSaveData component,
        string key,
        double fallback)
    {
        ItemStateValueSaveData value = component?.values?.FirstOrDefault(candidate =>
            candidate != null && string.Equals(candidate.key, key, StringComparison.Ordinal));
        if (value == null)
            return fallback;
        return value.kind switch
        {
            ItemStateValueKind.Decimal => value.decimalValue,
            ItemStateValueKind.Integer => value.integerValue,
            ItemStateValueKind.String when double.TryParse(
                value.stringValue,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double parsed) => parsed,
            _ => fallback
        };
    }

    private static bool ReadBoolean(
        ItemInstanceComponentSaveData component,
        string key,
        bool fallback)
    {
        ItemStateValueSaveData value = component?.values?.FirstOrDefault(candidate =>
            candidate != null && string.Equals(candidate.key, key, StringComparison.Ordinal));
        return value == null
            ? fallback
            : value.kind switch
            {
                ItemStateValueKind.Boolean => value.booleanValue,
                ItemStateValueKind.Integer => value.integerValue != 0,
                ItemStateValueKind.String when bool.TryParse(
                    value.stringValue,
                    out bool parsed) => parsed,
                _ => fallback
            };
    }

    private readonly struct FreshnessProjection
    {
        public FreshnessProjection(
            bool hasValue,
            float remainingSeconds,
            bool preserved)
        {
            HasValue = hasValue;
            RemainingSeconds = remainingSeconds;
            Preserved = preserved;
        }

        public bool HasValue { get; }
        public float RemainingSeconds { get; }
        public bool Preserved { get; }
    }

    private readonly struct PendingAggregationRequest
    {
        public PendingAggregationRequest(
            string stackId,
            string sourceStackId,
            string ownerOperationId,
            ItemReservationPurpose purpose,
            string aggregationCohortId,
            string destinationId,
            Vector2Int destinationPosition)
        {
            StackId = stackId;
            SourceStackId = sourceStackId?.Trim() ?? string.Empty;
            OwnerOperationId = ownerOperationId?.Trim() ?? string.Empty;
            Purpose = purpose;
            AggregationCohortId = aggregationCohortId;
            DestinationId = destinationId;
            DestinationPosition = destinationPosition;
        }

        public string StackId { get; }
        public string SourceStackId { get; }
        public string OwnerOperationId { get; }
        public ItemReservationPurpose Purpose { get; }
        public string AggregationCohortId { get; }
        public string DestinationId { get; }
        public Vector2Int DestinationPosition { get; }
    }
}
