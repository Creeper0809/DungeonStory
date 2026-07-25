#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

internal sealed class EditorNullItemMarkerPresenter : IItemMarkerPresenter
{
    public static readonly EditorNullItemMarkerPresenter Instance =
        new EditorNullItemMarkerPresenter();

    private EditorNullItemMarkerPresenter()
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

    public bool TryGetMarkerAt(Vector2Int position, out Object marker)
    {
        marker = null;
        return false;
    }

    public void Clear()
    {
    }
}
#endif
