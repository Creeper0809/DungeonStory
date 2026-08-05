using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BuildingConnectivityQueryAdapter :
    IBuildingConnectivityQueryPort
{
    private readonly Grid grid;

    public BuildingConnectivityQueryAdapter(Grid grid)
    {
        this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
    }

    public bool IsConnectedWithAny(IReadOnlyCollection<Vector2Int> positions) =>
        grid.IsConnectedWithAny(positions);

    public bool IsConnected(Vector2Int start, int associatedId) =>
        grid.IsConnected(start, associatedId);
}
