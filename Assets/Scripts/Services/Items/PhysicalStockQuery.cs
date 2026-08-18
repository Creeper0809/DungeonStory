using System;
using System.Linq;
using UnityEngine;
using DungeonStory.Balance;

public interface IStockQuery : IWarehousePhysicalStockQueryPort
{
    System.Collections.Generic.IReadOnlyList<WorldItemStackSnapshot> GetAllStacks();
    int GetGlobalQuantity(string itemDefinitionId);
    int GetWarehouseQuantity(
        BuildingInstanceId warehouseId,
        string itemDefinitionId);
}

/// <summary>
/// Rebuildable read index over authoritative physical item stacks. It owns no stock and
/// deliberately has no save DTO.
/// </summary>
public sealed class PhysicalStockQuery : IStockQuery
{
    private readonly WorldItemRepository repository;
    private readonly IDungeonItemCatalogProvider catalog;

    public PhysicalStockQuery(
        WorldItemRepository repository,
        IDungeonItemCatalogProvider catalog)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public System.Collections.Generic.IReadOnlyList<WorldItemStackSnapshot>
        GetAllStacks() => repository.Records
            .Where(record => record != null && record.quantity > 0)
            .Select(CreateSnapshot)
            .ToArray();

    public int GetGlobalQuantity(string itemDefinitionId)
    {
        string itemId = RequireItemId(itemDefinitionId);
        return SaturatingSum(repository.Records
            .Where(record => record != null
                && record.quantity > 0
                && string.Equals(record.itemId, itemId, StringComparison.Ordinal))
            .Select(record => record.quantity));
    }

    public int GetWarehouseQuantity(
        BuildingInstanceId warehouseId,
        string itemDefinitionId)
    {
        string itemId = RequireItemId(itemDefinitionId);
        string destinationId = RequireWarehouseDestinationId(warehouseId);
        return SaturatingSum(GetWarehouseRecords(destinationId)
            .Where(record => string.Equals(
                record.itemId,
                itemId,
                StringComparison.Ordinal))
            .Select(record => record.quantity));
    }

    public int GetWarehouseQuantity(
        BuildingInstanceId warehouseId,
        StockCategory category)
    {
        string destinationId = RequireWarehouseDestinationId(warehouseId);
        return SaturatingSum(GetWarehouseRecords(destinationId)
            .Where(record => catalog.GetDefinition(record.itemId).StockCategory == category)
            .Select(record => record.quantity));
    }

    public int GetWarehouseTotal(BuildingInstanceId warehouseId)
    {
        string destinationId = RequireWarehouseDestinationId(warehouseId);
        return SaturatingSum(GetWarehouseRecords(destinationId)
            .Select(record => record.quantity));
    }

    private System.Collections.Generic.IEnumerable<WorldItemStackRecord>
        GetWarehouseRecords(string destinationId)
    {
        return repository.Records.Where(record => record != null
            && record.quantity > 0
            && record.state == WorldItemStackState.Stored
            && string.Equals(
                string.IsNullOrWhiteSpace(record.sourceStorageDestinationId)
                    ? record.destinationId
                    : record.sourceStorageDestinationId,
                destinationId,
                StringComparison.Ordinal));
    }

    private WorldItemStackSnapshot CreateSnapshot(WorldItemStackRecord record)
    {
        DungeonItemDefinition definition = catalog.GetDefinition(record.itemId);
        return new WorldItemStackSnapshot
        {
            StackId = record.stackId,
            ContentRevision = repository.ItemStackVersion,
            ReservationRevision = record.reservationRevision,
            ItemInstanceId = record.itemInstanceId,
            ItemId = record.itemId,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            StockCategory = definition.StockCategory,
            Quantity = record.quantity,
            ReservedQuantity = ResolveReservedQuantity(record),
            UnitPrice = definition.UnitPrice,
            UnitWeight = definition.UnitWeight,
            Sprite = definition.Sprite,
            State = record.state,
            Position = record.position,
            ReservedByPersistentId = record.reservedByPersistentId,
            DestinationId = record.destinationId,
            AggregationCohortId = record.aggregationCohortId,
            SourceStorageDestinationId = record.sourceStorageDestinationId,
            HasDestinationPosition = record.hasDestinationPosition,
            DestinationPosition = record.destinationPosition,
            Forbidden = record.forbidden,
            SourceCharacterId = record.sourceCharacterId,
            SourceDisplayName = record.sourceDisplayName,
            SourceSpeciesTag = record.sourceSpeciesTag,
            SourceDeathReason = record.sourceDeathReason,
            EmergencyButcheryAllowed = record.emergencyButcheryAllowed,
            WasteOrigin = record.wasteOrigin,
            Contamination = record.contamination,
            Components = record.components
                .Where(component => component != null)
                .Select(component => component.Clone())
                .ToArray()
        };
    }

    private static int ResolveReservedQuantity(WorldItemStackRecord record) =>
        Mathf.Clamp(
            record.reservedQuantity,
            0,
            Mathf.Max(0, record.quantity));

    private string RequireItemId(string itemDefinitionId)
    {
        string itemId = itemDefinitionId?.Trim() ?? string.Empty;
        if (!catalog.TryGetDefinition(itemId, out _))
        {
            throw new System.Collections.Generic.KeyNotFoundException(
                $"Unknown physical item definition '{itemId}'.");
        }

        return itemId;
    }

    private static string RequireWarehouseDestinationId(
        BuildingInstanceId warehouseId)
    {
        if (!warehouseId.IsValid)
        {
            throw new ArgumentException(
                "A valid warehouse BuildingInstanceId is required.",
                nameof(warehouseId));
        }

        return WorldItemStackRuntime.WarehouseStorageDestinationPrefix
            + warehouseId.Value;
    }

    private static int SaturatingSum(System.Collections.Generic.IEnumerable<int> values)
    {
        long total = 0L;
        foreach (int value in values)
        {
            total += Math.Max(0, value);
            if (total >= int.MaxValue)
            {
                return int.MaxValue;
            }
        }

        return (int)total;
    }
}

/// <summary>
/// Derived diagnostic observer for physical loose stacks. It owns only first-seen
/// timestamps and never changes stock, destinations, walkability, or traversal cost.
/// </summary>
public sealed class FloorClutterDiagnosticsQuery : IFloorClutterDiagnosticsQuery
{
    private readonly IStockQuery stock;
    private readonly System.Collections.Generic.Dictionary<string, Observation> observations =
        new(System.StringComparer.Ordinal);

    private readonly struct Observation
    {
        internal Observation(Vector2Int position, float firstSeenAt)
        {
            Position = position;
            FirstSeenAt = firstSeenAt;
        }

        internal Vector2Int Position { get; }
        internal float FirstSeenAt { get; }
    }

    public FloorClutterDiagnosticsQuery(IStockQuery stock)
    {
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
    }

    public FloorClutterAssessment Capture(
        Grid grid,
        DungeonSpaceLayoutSnapshot layout,
        float currentGameTime)
    {
        if (grid == null)
            throw new ArgumentNullException(nameof(grid));
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));
        if (currentGameTime < 0f || float.IsNaN(currentGameTime)
            || float.IsInfinity(currentGameTime))
            throw new ArgumentOutOfRangeException(nameof(currentGameTime));

        float graceSeconds = Mathf.Min(
            layout.GameDaySeconds * 0.25f,
            Mathf.Max(
                15f,
                layout.CleanRunP95HaulDispatchAndDeliverySeconds * 2f));
        System.Collections.Generic.HashSet<string> liveCandidates =
            new(System.StringComparer.Ordinal);
        System.Collections.Generic.List<FloorClutterStackAssessment> outside = new();
        int looseStacks = 0;
        int looseQuantity = 0;
        foreach (WorldItemStackSnapshot stack in stock.GetAllStacks())
        {
            if (stack == null
                || stack.State != WorldItemStackState.Loose
                || stack.Quantity <= 0
                || !string.IsNullOrEmpty(stack.DestinationId))
                continue;
            looseStacks++;
            looseQuantity = checked(looseQuantity + stack.Quantity);
            SpatialCellRole roles = layout.GetRoles(stack.Position);
            if ((roles & (SpatialCellRole.StorageBuffer
                    | SpatialCellRole.OverflowContainment
                    | SpatialCellRole.AuthorizedLooseSource)) != 0)
                continue;

            liveCandidates.Add(stack.StackId);
            if (!observations.TryGetValue(stack.StackId, out Observation observation)
                || observation.Position != stack.Position
                || currentGameTime < observation.FirstSeenAt)
            {
                observation = new Observation(stack.Position, currentGameTime);
                observations[stack.StackId] = observation;
            }
            float age = currentGameTime - observation.FirstSeenAt;
            bool immediate = (roles & SpatialCellRole.EmergencyEgress) != 0
                || layout.IsCriticalAccess(stack.Position);
            outside.Add(new FloorClutterStackAssessment(
                stack.StackId,
                stack.Position,
                stack.Quantity,
                age,
                roles,
                immediate,
                immediate || age > graceSeconds));
        }

        foreach (string stale in observations.Keys
                     .Where(value => !liveCandidates.Contains(value))
                     .ToArray())
            observations.Remove(stale);
        outside.Sort((left, right) =>
            string.CompareOrdinal(left.StackId, right.StackId));
        return new FloorClutterAssessment(
            graceSeconds,
            looseStacks,
            looseQuantity,
            outside);
    }
}
