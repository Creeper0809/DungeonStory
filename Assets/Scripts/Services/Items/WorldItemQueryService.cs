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
}

public sealed class WorldItemQueryService : IWorldItemQueryService
{
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly WorldItemRepository repository;
    private readonly IItemMarkerPresenter markerPresenter;
    private bool storedItemMarkersVisible;

    public WorldItemQueryService(
        IDungeonItemCatalogProvider catalogProvider,
        WorldItemRepository repository,
        IItemMarkerPresenter markerPresenter)
    {
        this.catalogProvider = catalogProvider
            ?? throw new ArgumentNullException(nameof(catalogProvider));
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
            .ThenBy(stack => stack.IsReserved ? 1 : 0)
            .ThenByDescending(stack => stack.TotalValue)
            .ThenBy(stack => stack.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks()
    {
        return repository.Records
            .Where(stack => stack != null && stack.quantity > 0)
            .Select(CreateSnapshot)
            .ToArray();
    }

    internal WorldItemStackSnapshot CreateSnapshot(
        WorldItemStackRecord stack)
    {
        DungeonItemDefinition definition =
            catalogProvider.GetDefinition(stack.itemId);
        return new WorldItemStackSnapshot
        {
            StackId = stack.stackId,
            ItemId = stack.itemId,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            StockCategory = definition.StockCategory,
            Quantity = Mathf.Max(0, stack.quantity),
            UnitPrice = definition.UnitPrice,
            UnitWeight = definition.UnitWeight,
            Sprite = definition.Sprite,
            State = stack.state,
            Position = stack.position,
            ReservedByPersistentId =
                stack.reservedByPersistentId ?? string.Empty,
            DestinationId = stack.destinationId ?? string.Empty,
            SourceStorageDestinationId =
                stack.sourceStorageDestinationId ?? string.Empty,
            HasDestinationPosition = stack.hasDestinationPosition,
            DestinationPosition = stack.destinationPosition,
            Forbidden = stack.forbidden,
            SourceCharacterId = stack.sourceCharacterId ?? string.Empty,
            SourceDisplayName = stack.sourceDisplayName ?? string.Empty,
            SourceSpeciesTag = stack.sourceSpeciesTag ?? string.Empty,
            SourceDeathReason = stack.sourceDeathReason ?? string.Empty,
            EmergencyButcheryAllowed = stack.emergencyButcheryAllowed
        };
    }

    private static bool IsVisibleState(
        WorldItemStackState state,
        bool includeStored)
    {
        return state == WorldItemStackState.Loose
            || state == WorldItemStackState.FacilityBuffer
            || state == WorldItemStackState.ExpeditionPacked
            || (includeStored && state == WorldItemStackState.Stored);
    }

    private static int GetStateSortOrder(WorldItemStackSnapshot stack)
    {
        return stack.State switch
        {
            WorldItemStackState.Loose => 0,
            WorldItemStackState.FacilityBuffer => 1,
            WorldItemStackState.ExpeditionPacked => 2,
            WorldItemStackState.Stored => 3,
            _ => 3
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
