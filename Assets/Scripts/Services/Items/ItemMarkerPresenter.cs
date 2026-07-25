using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public interface IWorldItemMarkerDataSource
{
    bool TryGetPileAt(Vector2Int position, out WorldItemPileSnapshot pile);
}

public interface IItemMarkerPresenter
{
    void Initialize(IWorldItemMarkerDataSource dataSource);
    void RefreshAll(IEnumerable<Vector2Int> positions);
    void RefreshAt(Vector2Int position);
    bool TryGetMarkerAt(Vector2Int position, out UnityEngine.Object marker);
    void Clear();
}

public sealed class ItemMarkerPresenter : IItemMarkerPresenter, IDisposable
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IMainCameraProvider mainCameraProvider;
    private readonly ITmpKoreanFontService fontService;
    private readonly Dictionary<Vector2Int, WorldItemStackMarker> markersByPosition =
        new Dictionary<Vector2Int, WorldItemStackMarker>();

    private IWorldItemMarkerDataSource dataSource;

    public ItemMarkerPresenter(
        IGridSystemProvider gridSystemProvider,
        IMainCameraProvider mainCameraProvider,
        ITmpKoreanFontService fontService)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.mainCameraProvider = mainCameraProvider
            ?? throw new ArgumentNullException(nameof(mainCameraProvider));
        this.fontService = fontService
            ?? throw new ArgumentNullException(nameof(fontService));
    }

    public void Initialize(IWorldItemMarkerDataSource source)
    {
        dataSource = source ?? throw new ArgumentNullException(nameof(source));
    }

    public void RefreshAll(IEnumerable<Vector2Int> positions)
    {
        HashSet<Vector2Int> desired = positions != null
            ? new HashSet<Vector2Int>(positions)
            : new HashSet<Vector2Int>();

        foreach (Vector2Int stale in markersByPosition.Keys
                     .Where(position => !desired.Contains(position))
                     .ToArray())
        {
            RemoveMarker(stale);
        }

        foreach (Vector2Int position in desired)
        {
            RefreshAt(position);
        }
    }

    public void RefreshAt(Vector2Int position)
    {
        if (dataSource == null
            || !gridSystemProvider.TryGetGrid(out Grid grid)
            || !dataSource.TryGetPileAt(position, out WorldItemPileSnapshot pile))
        {
            RemoveMarker(position);
            return;
        }

        if (!markersByPosition.TryGetValue(position, out WorldItemStackMarker marker)
            || marker == null)
        {
            marker = WorldItemStackMarker.Create(
                dataSource,
                mainCameraProvider,
                fontService.Resolve(),
                grid,
                position);
            markersByPosition[position] = marker;
        }

        marker.Refresh(pile);
    }

    public bool TryGetMarkerAt(Vector2Int position, out UnityEngine.Object marker)
    {
        if (markersByPosition.TryGetValue(position, out WorldItemStackMarker found)
            && found != null)
        {
            marker = found;
            return true;
        }

        marker = null;
        return false;
    }

    public void Clear()
    {
        foreach (Vector2Int position in markersByPosition.Keys.ToArray())
        {
            RemoveMarker(position);
        }
    }

    public void Dispose()
    {
        Clear();
        dataSource = null;
    }

    private void RemoveMarker(Vector2Int position)
    {
        if (!markersByPosition.TryGetValue(position, out WorldItemStackMarker marker))
        {
            return;
        }

        markersByPosition.Remove(position);
        if (marker == null)
        {
            return;
        }

        if (gridSystemProvider.TryGetGrid(out Grid grid))
        {
            grid.RemoveOccupant(marker, GridLayer.Item, new[] { position }, disconnectPositions: false);
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(marker.gameObject);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(marker.gameObject);
        }
    }
}

internal sealed class NullItemMarkerPresenter : IItemMarkerPresenter
{
    public static readonly NullItemMarkerPresenter Instance = new NullItemMarkerPresenter();

    private NullItemMarkerPresenter()
    {
    }

    public void Initialize(IWorldItemMarkerDataSource dataSource)
    {
    }

    public void RefreshAll(IEnumerable<Vector2Int> positions)
    {
    }

    public void RefreshAt(Vector2Int position)
    {
    }

    public bool TryGetMarkerAt(Vector2Int position, out UnityEngine.Object marker)
    {
        marker = null;
        return false;
    }

    public void Clear()
    {
    }
}
