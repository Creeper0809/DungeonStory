using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class CharacterBreakdownWorld
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWorldFilthQuery filthQuery;
    private readonly IWorldWaterQuery waterQuery;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly ICharacterAiWorldRegistry worldRegistry;

    public CharacterBreakdownWorld(
        IGridSystemProvider gridSystemProvider,
        IWorldItemStackRuntime itemStackRuntime,
        IWorldFilthQuery filthQuery,
        IWorldWaterQuery waterQuery,
        IRoomLayoutCache roomLayoutCache,
        ICharacterAiWorldRegistry worldRegistry)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.filthQuery = filthQuery
            ?? throw new ArgumentNullException(nameof(filthQuery));
        this.waterQuery = waterQuery
            ?? throw new ArgumentNullException(nameof(waterQuery));
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
    }

    public IReadOnlyList<CharacterActor> Characters => worldRegistry.Characters;
    public IReadOnlyList<BuildableObject> Buildings => worldRegistry.Buildings;

    public bool TryGetGrid(out Grid grid)
    {
        return gridSystemProvider.TryGetGrid(out grid);
    }

    public bool TryFindBestAvailableStack(
        Vector2Int origin,
        Func<string, int> rankSelector,
        out WorldItemStackSnapshot stack)
    {
        return itemStackRuntime.TryFindBestAvailableStack(
            origin,
            rankSelector,
            out stack);
    }

    public bool TryConsumeStack(
        string stackId,
        int quantity,
        out WorldItemStackSnapshot consumed)
    {
        return itemStackRuntime.TryConsumeStackQuantity(
            stackId,
            quantity,
            out consumed);
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetStacksAt(
        Vector2Int position,
        bool includeStored)
    {
        return itemStackRuntime.GetStacksAt(position, includeStored);
    }

    public void AddFilth(
        WorldFilthType type,
        Vector2Int position,
        float amount,
        string sourceId,
        float contamination,
        bool wallStain = false)
    {
        filthQuery.AddFilth(
            type,
            position,
            amount,
            sourceId,
            contamination,
            wallStain);
    }

    public bool TryFindDrinkSource(
        Vector2Int origin,
        bool allowFoul,
        out WorldWaterSourceSnapshot source)
    {
        return waterQuery.TryFindDrinkSource(origin, allowFoul, out source);
    }

    public bool TryDrink(
        string sourceId,
        float amount,
        out WorldWaterQuality quality,
        out float consumed)
    {
        return waterQuery.TryDrink(sourceId, amount, out quality, out consumed);
    }

    public int GetAccidentLocationPriority(Grid grid, GridCell cell)
    {
        if (cell.AreaType == GridCellAreaType.ExteriorPath)
        {
            return 0;
        }

        if (cell.HasOccupantInLayer(GridLayer.Hallway))
        {
            return 100;
        }

        return roomLayoutCache.TryGetRoom(
                grid,
                cell.Position,
                out RoomInstance room)
            ? 200 + Mathf.RoundToInt(room.GetQualityScore() * 100f)
            : 350;
    }
}
