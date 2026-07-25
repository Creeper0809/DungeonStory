using System;
using UnityEngine;

public interface IWorkGridResolver
{
    Grid ResolveActiveGrid(
        AbilityWork work,
        GridPathSearchResult searchResult,
        Grid priorityGrid = null);

    Vector2Int GetGridPosition(Grid activeGrid, CharacterActor actor);
}

public sealed class WorkGridResolver : IWorkGridResolver
{
    private readonly IGridSystemProvider gridSystemProvider;

    public WorkGridResolver(IGridSystemProvider gridSystemProvider)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
    }

    public Grid ResolveActiveGrid(
        AbilityWork work,
        GridPathSearchResult searchResult,
        Grid priorityGrid = null)
    {
        if (searchResult != null && searchResult.sourceGrid != null)
        {
            return searchResult.sourceGrid;
        }

        if (priorityGrid != null)
        {
            return priorityGrid;
        }

        if (gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return grid;
        }

        work?.EnsureWorkReferences();
        return work != null ? work.CachedGrid : null;
    }

    public Vector2Int GetGridPosition(Grid activeGrid, CharacterActor actor)
    {
        if (activeGrid == null || actor == null)
        {
            return Vector2Int.zero;
        }

        Vector2Int position = activeGrid.GetXY(actor.transform.position);
        return activeGrid.IsValidGridPos(position) ? position : Vector2Int.zero;
    }
}
