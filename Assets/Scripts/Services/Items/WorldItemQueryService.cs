using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IWorldItemQueryService
{
    bool StoredItemMarkersVisible { get; }
    void SetStoredItemMarkersVisible(bool visible);
    bool TryGetPileAt(Vector2Int position, out WorldItemPileSnapshot pile);
    bool TryGetPileTargetAt(
        Vector2Int position,
        out ItemPileInfoTarget target,
        out UnityEngine.Object markerObject);
    IReadOnlyList<WorldItemStackSnapshot> GetStacksAt(
        Vector2Int position,
        bool includeStored = false);
    IReadOnlyList<WorldItemStackSnapshot> GetAllStacks();
    bool TryFindNearestAvailableStock(
        Vector2Int origin,
        StockCategory category,
        bool preferStored,
        out WorldItemStackSnapshot stack);
    void CopyAvailableStockCandidates(
        StockCategory category,
        List<WorldItemStockCandidate> destination);
    bool TryFindBestAvailableStack(
        Vector2Int origin,
        Func<string, int> rankSelector,
        out WorldItemStackSnapshot stack);
}

public sealed class WorldItemQueryService : IWorldItemQueryService
{
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly WorldItemRepository repository;
    private readonly IItemMarkerPresenter markerPresenter;
    private bool storedItemMarkersVisible;
    private int allStacksCacheVersion = -1;
    private IReadOnlyList<WorldItemStackSnapshot> allStacksCache =
        Array.Empty<WorldItemStackSnapshot>();

    public WorldItemQueryService(
        IDungeonItemCatalogProvider catalogProvider,
        IPhysicalItemMassQuery massQuery,
        WorldItemRepository repository,
        IItemMarkerPresenter markerPresenter)
    {
        this.catalogProvider = catalogProvider
            ?? throw new ArgumentNullException(nameof(catalogProvider));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.markerPresenter = markerPresenter
            ?? throw new ArgumentNullException(nameof(markerPresenter));
    }

    public bool StoredItemMarkersVisible => storedItemMarkersVisible;

    public void SetStoredItemMarkersVisible(bool visible)
    {
        if (storedItemMarkersVisible == visible)
        {
            return;
        }

        storedItemMarkersVisible = visible;
        markerPresenter.RefreshAll(repository.Records
            .Where(stack => stack != null)
            .Select(stack => stack.position)
            .Distinct()
            .ToArray());
    }

    public bool TryGetPileAt(
        Vector2Int position,
        out WorldItemPileSnapshot pile)
    {
        IReadOnlyList<WorldItemStackSnapshot> snapshots = GetStacksAt(
            position,
            storedItemMarkersVisible);
        if (snapshots.Count == 0)
        {
            pile = null;
            return false;
        }

        pile = new WorldItemPileSnapshot
        {
            Position = position,
            Stacks = snapshots,
            Representative = SelectRepresentative(snapshots)
        };
        return true;
    }

    public bool TryGetPileTargetAt(
        Vector2Int position,
        out ItemPileInfoTarget target,
        out UnityEngine.Object markerObject)
    {
        target = null;
        markerObject = null;
        if (!TryGetPileAt(position, out _))
        {
            return false;
        }

        target = new ItemPileInfoTarget(position);
        markerPresenter.TryGetMarkerAt(position, out markerObject);
        return true;
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetStacksAt(
        Vector2Int position,
        bool includeStored = false)
    {
        if (!repository.RecordsByPosition.TryGetValue(
                position,
                out List<WorldItemStackRecord> records)
            || records == null
            || records.Count == 0)
        {
            return Array.Empty<WorldItemStackSnapshot>();
        }

        return records
            .Where(stack => stack != null
                && stack.quantity > 0
                && IsVisibleState(stack.state, includeStored))
            .Select(CreateSnapshot)
            .OrderBy(GetStateSortOrder)
            .ThenBy(stack => stack.IsFullyReserved ? 1 : 0)
            .ThenByDescending(stack => stack.TotalValue)
            .ThenBy(stack => stack.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks()
    {
        int version = repository.ItemStackVersion;
        if (allStacksCacheVersion == version)
        {
            return allStacksCache;
        }

        List<WorldItemStackRecord> records = repository.Records;
        List<WorldItemStackSnapshot> snapshots =
            new List<WorldItemStackSnapshot>(records.Count);
        for (int index = 0; index < records.Count; index++)
        {
            WorldItemStackRecord record = records[index];
            if (record != null && record.quantity > 0)
            {
                snapshots.Add(CreateSnapshot(record));
            }
        }

        allStacksCache = snapshots.Count > 0
            ? snapshots.ToArray()
            : Array.Empty<WorldItemStackSnapshot>();
        allStacksCacheVersion = version;
        return allStacksCache;
    }

    public bool TryFindNearestAvailableStock(
        Vector2Int origin,
        StockCategory category,
        bool preferStored,
        out WorldItemStackSnapshot stack)
    {
        WorldItemStackRecord best = null;
        int bestStateRank = int.MaxValue;
        int bestDistance = int.MaxValue;
        List<WorldItemStackRecord> records = repository.Records;
        for (int index = 0; index < records.Count; index++)
        {
            WorldItemStackRecord candidate = records[index];
            if (!IsConsumableStock(candidate)
                || catalogProvider.GetDefinition(candidate.itemId).StockCategory
                    != category)
            {
                continue;
            }

            int stateRank = preferStored
                && candidate.state == WorldItemStackState.Stored
                    ? 0
                    : 1;
            int distance = Manhattan(origin, candidate.position);
            if (stateRank > bestStateRank
                || (stateRank == bestStateRank && distance >= bestDistance))
            {
                continue;
            }

            best = candidate;
            bestStateRank = stateRank;
            bestDistance = distance;
        }

        stack = best != null ? CreateSnapshot(best) : null;
        return stack != null;
    }

    public void CopyAvailableStockCandidates(
        StockCategory category,
        List<WorldItemStockCandidate> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        List<WorldItemStackRecord> records = repository.Records;
        for (int index = 0; index < records.Count; index++)
        {
            WorldItemStackRecord candidate = records[index];
            if (!IsConsumableStock(candidate)
                || catalogProvider.GetDefinition(candidate.itemId).StockCategory
                    != category)
            {
                continue;
            }

            destination.Add(new WorldItemStockCandidate(
                candidate.stackId,
                candidate.position,
                candidate.state,
                candidate.quantity));
        }
    }

    public bool TryFindBestAvailableStack(
        Vector2Int origin,
        Func<string, int> rankSelector,
        out WorldItemStackSnapshot stack)
    {
        if (rankSelector == null)
        {
            throw new ArgumentNullException(nameof(rankSelector));
        }

        WorldItemStackRecord best = null;
        int bestRank = int.MaxValue;
        int bestDistance = int.MaxValue;
        List<WorldItemStackRecord> records = repository.Records;
        for (int index = 0; index < records.Count; index++)
        {
            WorldItemStackRecord candidate = records[index];
            if (!IsConsumableStock(candidate))
            {
                continue;
            }

            int rank = rankSelector(candidate.itemId);
            if (rank == int.MaxValue)
            {
                continue;
            }

            int distance = Manhattan(origin, candidate.position);
            if (rank > bestRank
                || (rank == bestRank && distance >= bestDistance))
            {
                continue;
            }

            best = candidate;
            bestRank = rank;
            bestDistance = distance;
        }

        stack = best != null ? CreateSnapshot(best) : null;
        return stack != null;
    }

    private static bool IsAvailable(WorldItemStackRecord stack)
    {
        return stack != null
            && stack.quantity > 0
            && !stack.forbidden
            && stack.quantity - stack.reservedQuantity > 0;
    }

    private static bool IsConsumableStock(WorldItemStackRecord stack)
    {
        if (!IsAvailable(stack))
        {
            return false;
        }

        if (stack.state == WorldItemStackState.Loose)
        {
            return string.IsNullOrWhiteSpace(stack.destinationId);
        }

        if (stack.state != WorldItemStackState.Stored)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(stack.destinationId)
            || stack.destinationId.StartsWith(
                WorldItemStackRuntime.WarehouseStorageDestinationPrefix,
                StringComparison.Ordinal);
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }

    internal WorldItemStackSnapshot CreateSnapshot(
        WorldItemStackRecord stack)
    {
        DungeonItemDefinition definition =
            catalogProvider.GetDefinition(stack.itemId);
        return new WorldItemStackSnapshot
        {
            StackId = stack.stackId,
            ContentRevision = repository.ItemStackVersion,
            ReservationRevision = stack.reservationRevision,
            ItemInstanceId = stack.itemInstanceId,
            ItemId = stack.itemId,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            StockCategory = definition.StockCategory,
            Quantity = Mathf.Max(0, stack.quantity),
            ReservedQuantity = Mathf.Clamp(
                stack.reservedQuantity,
                0,
                Mathf.Max(0, stack.quantity)),
            UnitPrice = definition.UnitPrice,
            UnitWeight = massQuery
                .GetDefinitionUnitMass((ItemDefinitionId)stack.itemId)
                .Value / 1000f,
            Sprite = definition.Sprite,
            State = stack.state,
            Position = stack.position,
            ReservedByPersistentId =
                stack.reservedByPersistentId ?? string.Empty,
            DestinationId = stack.destinationId ?? string.Empty,
            AggregationCohortId = stack.aggregationCohortId ?? string.Empty,
            SourceStorageDestinationId =
                stack.sourceStorageDestinationId ?? string.Empty,
            HasDestinationPosition = stack.hasDestinationPosition,
            DestinationPosition = stack.destinationPosition,
            Forbidden = stack.forbidden,
            SourceCharacterId = stack.sourceCharacterId ?? string.Empty,
            SourceDisplayName = stack.sourceDisplayName ?? string.Empty,
            SourceSpeciesTag = stack.sourceSpeciesTag ?? string.Empty,
            SourceDeathReason = stack.sourceDeathReason ?? string.Empty,
            EmergencyButcheryAllowed = stack.emergencyButcheryAllowed,
            WasteOrigin = stack.wasteOrigin,
            Contamination = Mathf.Clamp(stack.contamination, 0f, 100f),
            DropDisposition = stack.dropDisposition,
            RecoveryOwnerOperationId = stack.recoveryOwnerOperationId ?? string.Empty,
            RecoverySourceStackId = stack.recoverySourceStackId ?? string.Empty,
            RecoveryCarrierPersistentId = stack.recoveryCarrierPersistentId ?? string.Empty,
            RecoveryInterruptionKind = stack.recoveryInterruptionKind,
            DroppedAtGameTime = stack.droppedAtGameTime,
            RecoveryDeadlineGameTime = stack.recoveryDeadlineGameTime,
            Components = (stack.components ?? new List<ItemInstanceComponentSaveData>())
                .Where(component => component != null)
                .Select(component => component.Clone())
                .ToArray()
        };
    }

    private static bool IsVisibleState(
        WorldItemStackState state,
        bool includeStored)
    {
        return state == WorldItemStackState.Loose
            || state == WorldItemStackState.FacilityBuffer
            || state == WorldItemStackState.FacilityOutputBuffer
            || state == WorldItemStackState.ExpeditionPacked
            || (includeStored && state == WorldItemStackState.Stored);
    }

    private static int GetStateSortOrder(WorldItemStackSnapshot stack)
    {
        return stack.State switch
        {
            WorldItemStackState.Loose => 0,
            WorldItemStackState.FacilityOutputBuffer => 1,
            WorldItemStackState.FacilityBuffer => 2,
            WorldItemStackState.ExpeditionPacked => 3,
            WorldItemStackState.Stored => 4,
            _ => 4
        };
    }

    private static WorldItemStackSnapshot SelectRepresentative(
        IReadOnlyList<WorldItemStackSnapshot> stacks)
    {
        return stacks
            .OrderBy(GetStateSortOrder)
            .ThenByDescending(stack => stack.TotalValue)
            .ThenBy(stack => stack.DisplayName, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
