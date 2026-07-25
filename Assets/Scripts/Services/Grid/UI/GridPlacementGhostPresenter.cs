using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GridGhostObject))]
public abstract class GridPlacementGhostPresenter : MonoBehaviour
{
    [SerializeField] private GridGhostObject ghostObject;
    [SerializeField] private int cellWorldHeight = 3;

    private IGridGhostObjectResolver ghostObjectResolver;

    protected abstract Grid ActiveGrid { get; }
    protected abstract bool HasGhostSelection { get; }
    protected abstract Sprite GhostSprite { get; }
    protected abstract int GhostWidth { get; }
    protected abstract int GhostHeight { get; }
    protected abstract float GridOriginZ { get; }
    protected abstract bool IsDraggingSelection { get; }
    protected abstract IReadOnlyList<Vector2Int> SelectedGridPositions { get; }
    protected abstract Vector3 MouseWorldPosition { get; }
    protected abstract Vector3 MouseWorldPositionSnapped { get; }
    protected virtual bool GhostUsesFullFootprint => false;
    protected virtual Vector2 GhostVisualOffset => Vector2.zero;
    protected virtual Vector2? GhostVisualSizeOverride => null;

    protected virtual int CellWorldHeight => cellWorldHeight;

    protected abstract bool CanPlaceAt(Vector2Int gridPosition);

    protected virtual void Awake()
    {
        if (TryInitializeLocalGhostObject())
        {
            ghostObject.Hide();
        }
    }

    protected virtual void Update()
    {
        UpdateGhost();
    }

    protected void RefreshGhostSelection()
    {
        if (!HasGhostSelection)
        {
            HideGhost();
            return;
        }

        EnsureGhostObject();
        ghostObject.SetSize(GetGhostVisualSize());
        ghostObject.Show(GhostSprite);
        ghostObject.SetWorldPosition(MouseWorldPosition + (Vector3)GhostVisualOffset);
    }

    protected void HideGhost()
    {
        EnsureGhostObject();
        ghostObject?.Hide();
    }

    protected void ConstructGridPlacementGhostPresenter(IGridGhostObjectResolver ghostObjectResolver)
    {
        this.ghostObjectResolver = ghostObjectResolver
            ?? throw new ArgumentNullException(nameof(ghostObjectResolver));
        EnsureGhostObject();
        ghostObject.Hide();
    }

    private void UpdateGhost()
    {
        EnsureGhostObject();
        Grid grid = ActiveGrid;
        if (ghostObject == null || ghostObject.IsHidden || grid == null) return;

        if (!HasGhostSelection)
        {
            HideGhost();
            return;
        }

        if (!IsDraggingSelection)
        {
            UpdateSingleCellGhost(grid);
            return;
        }

        UpdateDraggedGhost(grid);
    }

    private void UpdateSingleCellGhost(Grid grid)
    {
        Vector2Int targetPos = grid.GetXY(MouseWorldPosition);
        ghostObject.SetBuildable(CanPlaceAt(targetPos));
        ghostObject.SetSize(GetGhostVisualSize());

        Vector3 snappedPosition = MouseWorldPositionSnapped;
        Vector2 offset = GhostWidth % 2 == 0 ? new Vector2(0.5f, 0f) : Vector2.zero;
        Vector2 ghostPosition = (Vector2)snappedPosition + offset + GhostVisualOffset;
        ghostObject.SetWorldPosition(new Vector3(ghostPosition.x, ghostPosition.y, GridOriginZ));
    }

    private void UpdateDraggedGhost(Grid grid)
    {
        IReadOnlyList<Vector2Int> selectedPositions = SelectedGridPositions;
        if (selectedPositions == null || selectedPositions.Count == 0) return;

        int width = Mathf.Max(1, GhostWidth);
        Vector3 evenWidthOffset = width % 2 == 0 ? new Vector3(0.5f, 0f, 0f) : Vector3.zero;
        List<Vector3> worldPositions = new List<Vector3>(selectedPositions.Count);
        List<bool> buildableStates = new List<bool>(selectedPositions.Count);

        foreach (Vector2Int selectedPosition in selectedPositions)
        {
            Vector3 worldPosition = grid.GetWorldPos(selectedPosition) + evenWidthOffset;
            worldPosition += (Vector3)GhostVisualOffset;
            worldPosition.z = GridOriginZ;
            worldPositions.Add(worldPosition);
            buildableStates.Add(CanPlaceAt(selectedPosition));
        }

        ghostObject.ShowRepeated(
            GhostSprite,
            worldPositions,
            GetGhostVisualSize(),
            buildableStates);
    }

    private Vector2 GetGhostVisualSize()
    {
        if (GhostVisualSizeOverride.HasValue)
        {
            return GhostVisualSizeOverride.Value;
        }

        Vector2 footprintSize = new Vector2(
            Mathf.Max(1, GhostWidth),
            Mathf.Max(1, GhostHeight) * CellWorldHeight);
        return GhostUsesFullFootprint
            ? footprintSize
            : GridBuildingTileTransformCalculator.GetVisualFootprintSize(footprintSize);
    }

    private void EnsureGhostObject()
    {
        if (ghostObject == null)
        {
            ghostObject = ghostObjectResolver != null
                ? ghostObjectResolver.Resolve(this, ghostObject)
                : GetComponent<GridGhostObject>();
        }

        if (ghostObject == null)
        {
            throw new InvalidOperationException(
                $"{nameof(GridPlacementGhostPresenter)} requires a serialized or attached {nameof(GridGhostObject)}.");
        }

        InitializeGhostObject();
    }

    private bool TryInitializeLocalGhostObject()
    {
        if (ghostObject == null)
        {
            ghostObject = GetComponent<GridGhostObject>();
        }

        if (ghostObject == null)
        {
            return false;
        }

        InitializeGhostObject();
        return true;
    }

    private void InitializeGhostObject()
    {
        ghostObject.Initialize(transform.childCount > 0 ? transform.GetChild(0).gameObject : null);
    }
}
