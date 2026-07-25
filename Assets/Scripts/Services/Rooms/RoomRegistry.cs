using System;
using System.Collections.Generic;

public interface IRoomLayoutCache
{
    RoomLayout GetLayout(Grid grid);
    bool TryGetRoom(Grid grid, UnityEngine.Vector2Int cell, out RoomInstance room);
    bool TryGetRoom(BuildableObject part, out RoomInstance room);
    void Clear();
}

public sealed class RoomLayoutCache : IRoomLayoutCache
{
    private sealed class CachedLayout
    {
        public int GridVersion = -1;
        public RoomLayout Layout;
    }

    private readonly Dictionary<Grid, CachedLayout> cacheByGrid =
        new Dictionary<Grid, CachedLayout>();

    public RoomLayout GetLayout(Grid grid)
    {
        if (grid == null)
        {
            return new RoomLayout(Array.Empty<RoomInstance>());
        }

        if (!cacheByGrid.TryGetValue(grid, out CachedLayout cache))
        {
            cache = new CachedLayout();
            cacheByGrid[grid] = cache;
        }

        if (cache.Layout == null || cache.GridVersion != grid.version)
        {
            cache.GridVersion = grid.version;
            cache.Layout = RoomDetector.Build(grid);
        }

        return cache.Layout;
    }

    public bool TryGetRoom(BuildableObject part, out RoomInstance room)
    {
        room = null;
        if (part == null || part.Grid == null)
        {
            return false;
        }

        return GetLayout(part.Grid).TryGetRoom(part, out room);
    }

    public bool TryGetRoom(Grid grid, UnityEngine.Vector2Int cell, out RoomInstance room)
    {
        room = null;
        return grid != null && GetLayout(grid).TryGetRoom(cell, out room);
    }

    public void Clear()
    {
        cacheByGrid.Clear();
    }
}
