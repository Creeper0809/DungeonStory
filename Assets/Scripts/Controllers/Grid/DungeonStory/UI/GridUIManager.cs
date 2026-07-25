using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using VContainer;

public class GridUIManager : MonoBehaviour
{
    private const string GridOverlaySortingLayerName = "Default";
    private const int GridOverlaySortingOrder = 100;

    private IGridSystemProvider gridSystemProvider;
    private IDungeonGridBuildingControllerProvider buildingControllerProvider;
    private IPlayerInputReader inputReader;
    private IUiPointerBlocker uiPointerBlocker;
    private IWorldInfoClickSelector worldInfoClickSelector;
    private GridSystemManager gridSystemManager;
    private DungeonStoryGridBuildingController buildingController;

    public GameObject gridTextureCanvas;
    public UIBuildingInfo buildingInfoUI;
    public GameObject constructPanel;
    public event Action OnUiClose;

    [Header("Placement Grid")]
    [SerializeField] private Color buildableCellColor = new Color(1f, 1f, 1f, 0.82f);
    [SerializeField] private Color blockedCellColor = new Color(0.85f, 0.15f, 0.15f, 0.10f);

    private Tilemap gridOverlayTilemap;
    private TilemapRenderer gridOverlayRenderer;
    private TileBase gridOverlayTile;
    private bool subscribed;
    private bool initialized;

    public int BuildableCellCount { get; private set; }
    public int BlockedCellCount { get; private set; }
    public bool IsGridVisible => gridTextureCanvas != null && gridTextureCanvas.activeInHierarchy;

    [Inject]
    public void Construct(
        IGridSystemProvider gridSystemProvider,
        IDungeonGridBuildingControllerProvider buildingControllerProvider,
        IPlayerInputReader inputReader,
        IUiPointerBlocker uiPointerBlocker,
        IWorldInfoClickSelector worldInfoClickSelector)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.buildingControllerProvider = buildingControllerProvider
            ?? throw new ArgumentNullException(nameof(buildingControllerProvider));
        this.inputReader = inputReader
            ?? throw new ArgumentNullException(nameof(inputReader));
        this.uiPointerBlocker = uiPointerBlocker
            ?? throw new ArgumentNullException(nameof(uiPointerBlocker));
        this.worldInfoClickSelector = worldInfoClickSelector
            ?? throw new ArgumentNullException(nameof(worldInfoClickSelector));
        InitializeRuntimeUiIfReady();
    }

    private void Start()
    {
        InitializeRuntimeUiIfReady();
    }

    private void Update()
    {
        if (!TryResolveRuntimeDependencies())
        {
            return;
        }

        if (gridSystemManager.Mode != GridMode.None
            && inputReader != null
            && inputReader.GetKeyDown(KeyCode.Escape))
        {
            buildingController.SetGridModeNone();
            return;
        }

    }

    private IWorldInfoClickSelector RequireWorldInfoClickSelector()
    {
        return worldInfoClickSelector
            ?? throw new InvalidOperationException(
                $"{nameof(GridUIManager)} requires {nameof(IWorldInfoClickSelector)} injection.");
    }

    public void ToggleConstructTab()
    {
        if (constructPanel.activeSelf)
        {
            CloseConstructTab();
        }
        else
        {
            ShowConstructTab();
        }
    }

    public void ShowConstructTab()
    {
        constructPanel.SetActive(true);
    }

    public void CloseConstructTab()
    {
        ResolveRuntimeDependencies();
        OnUiClose?.Invoke();
        buildingController.SetGridModeNone();
        constructPanel.SetActive(false);
    }

    public void HideGrid()
    {
        ResolveRuntimeDependencies();
        gridTextureCanvas.SetActive(false);
        BuildableCellCount = 0;
        BlockedCellCount = 0;
    }

    public void ShowGrid()
    {
        ResolveRuntimeDependencies();
        InitializeGridOverlay();
        gridTextureCanvas.SetActive(true);
        DrawGrid();
    }

    public void DrawGrid()
    {
        ResolveRuntimeDependencies();
        InitializeGridOverlay();

        Grid grid = gridSystemManager.grid;
        if (grid == null || gridOverlayTile == null)
        {
            return;
        }

        gridOverlayTilemap.ClearAllTiles();
        gridOverlayTilemap.color = Color.white;
        BuildableCellCount = 0;
        BlockedCellCount = 0;
        HashSet<Vector2Int> availableCells = GetAvailableOverlayCells(grid);

        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                Vector2Int gridPosition = new Vector2Int(x, y);
                bool isAvailable = availableCells.Contains(gridPosition);
                Color cellColor = isAvailable ? buildableCellColor : blockedCellColor;
                if (isAvailable)
                {
                    BuildableCellCount++;
                }
                else
                {
                    BlockedCellCount++;
                }

                for (int subY = 0; subY < GridBuildingTileTransformCalculator.DefaultCellTileHeight; subY++)
                {
                    Vector3Int tilePosition = GetOverlayTilePosition(gridPosition, subY);
                    gridOverlayTilemap.SetTile(tilePosition, gridOverlayTile);
                    gridOverlayTilemap.SetTileFlags(tilePosition, TileFlags.None);
                    gridOverlayTilemap.SetColor(tilePosition, cellColor);
                }
            }
        }
    }

    private void OnEnable()
    {
        InitializeRuntimeUiIfReady();
    }

    private void OnDisable()
    {
        UnsubscribeFromRuntimeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromRuntimeEvents();
    }

    private void OnGridModeChanged(GridMode mode)
    {
        RefreshGridVisibility(mode);
    }

    private void OnSelectedBuildingChanged(BuildingSO building)
    {
        if (gridSystemManager != null && gridSystemManager.Mode == GridMode.Build)
        {
            ShowGrid();
        }
    }

    private void OnGridChanged()
    {
        if (IsGridVisible)
        {
            DrawGrid();
        }
    }

    private void RefreshGridVisibility(GridMode mode)
    {
        if (mode == GridMode.Build || mode == GridMode.Destory)
        {
            ShowGrid();
        }
        else
        {
            HideGrid();
        }
    }

    private HashSet<Vector2Int> GetAvailableOverlayCells(Grid grid)
    {
        HashSet<Vector2Int> availableCells = new HashSet<Vector2Int>();
        if (gridSystemManager.Mode == GridMode.Destory)
        {
            foreach (GridCell cell in grid.GetCells())
            {
                if (cell?.GetBuilding() != null)
                {
                    availableCells.Add(cell.Position);
                }
            }

            return availableCells;
        }

        BuildingSO selectedBuilding = buildingController.SelectedBuilding;
        if (selectedBuilding != null && selectedBuilding.IsInteriorDoor)
        {
            return GridPlacementCellAvailability.CollectDoorInstallableCells(grid, sidePadding: 1);
        }

        GridLayer layer = selectedBuilding != null
            ? selectedBuilding.Placement.Layer
            : GridLayer.Building;
        return selectedBuilding != null
            ? GridPlacementCellAvailability.CollectInstallableCells(grid, selectedBuilding, sidePadding: 1)
            : GridPlacementCellAvailability.CollectInstallableCells(grid, layer, sidePadding: 1);
    }

    private static Vector3Int GetOverlayTilePosition(Vector2Int gridPosition, int subY)
    {
        return new Vector3Int(
            -gridPosition.x,
            gridPosition.y * GridBuildingTileTransformCalculator.DefaultCellTileHeight + subY,
            0);
    }

    private bool IsPointerOverUi()
    {
        return RequireUiPointerBlocker().IsPointerOverUi();
    }

    private void InitializeGridOverlay()
    {
        if (gridTextureCanvas == null)
        {
            throw new InvalidOperationException(
                $"{nameof(GridUIManager)} requires a grid texture canvas reference.");
        }

        if (gridOverlayTilemap == null)
        {
            gridOverlayTilemap = gridTextureCanvas.GetComponent<Tilemap>()
                ?? gridTextureCanvas.GetComponentInChildren<Tilemap>(true);
        }
        if (gridOverlayTilemap == null)
        {
            throw new InvalidOperationException(
                $"{nameof(GridUIManager)} grid texture canvas requires a {nameof(Tilemap)} component on itself or a child.");
        }

        if (gridOverlayRenderer == null)
        {
            gridOverlayRenderer = gridOverlayTilemap.GetComponent<TilemapRenderer>();
        }
        if (gridOverlayRenderer == null)
        {
            throw new InvalidOperationException(
                $"{nameof(GridUIManager)} grid overlay requires a {nameof(TilemapRenderer)} component.");
        }

        gridOverlayRenderer.sortingLayerName = GridOverlaySortingLayerName;
        gridOverlayRenderer.sortingOrder = GridOverlaySortingOrder;

        if (gridOverlayTile != null)
        {
            return;
        }

        foreach (Vector3Int position in gridOverlayTilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = gridOverlayTilemap.GetTile(position);
            if (tile == null)
            {
                continue;
            }

            gridOverlayTile = tile;
            break;
        }

        if (gridOverlayTile == null)
        {
            throw new InvalidOperationException(
                $"{nameof(GridUIManager)} grid overlay requires at least one source tile.");
        }
    }

    private void SubscribeToRuntimeEventsIfReady()
    {
        if (isActiveAndEnabled)
        {
            SubscribeToRuntimeEvents();
        }
    }

    private void SubscribeToRuntimeEvents()
    {
        if (!TryResolveRuntimeDependencies())
        {
            return;
        }

        if (subscribed)
        {
            return;
        }

        gridSystemManager.OnGridModeChanged += OnGridModeChanged;
        gridSystemManager.OnGridExpand += OnGridChanged;
        gridSystemManager.OnGridObjectChanged += OnGridChanged;
        buildingController.OnSelectedBuildingChanged += OnSelectedBuildingChanged;
        subscribed = true;
    }

    private void UnsubscribeFromRuntimeEvents()
    {
        if (!subscribed)
        {
            return;
        }

        gridSystemManager.OnGridModeChanged -= OnGridModeChanged;
        gridSystemManager.OnGridExpand -= OnGridChanged;
        gridSystemManager.OnGridObjectChanged -= OnGridChanged;
        buildingController.OnSelectedBuildingChanged -= OnSelectedBuildingChanged;
        subscribed = false;
    }

    private void ResolveRuntimeDependencies()
    {
        if (!TryResolveRuntimeDependencies())
        {
            throw new InvalidOperationException(
                $"{nameof(GridUIManager)} requires injected grid, building, input, and canvas dependencies.");
        }
    }

    private bool TryResolveRuntimeDependencies()
    {
        if (gridSystemProvider == null || buildingControllerProvider == null || inputReader == null)
        {
            return false;
        }

        gridSystemManager = gridSystemProvider.Manager;
        buildingController = buildingControllerProvider.Controller;
        if (gridSystemManager == null || buildingController == null)
        {
            return false;
        }

        if (gridTextureCanvas == null)
        {
            return false;
        }

        return true;
    }

    private bool InitializeRuntimeUiIfReady()
    {
        if (!TryResolveRuntimeDependencies())
        {
            return false;
        }

        InitializeGridOverlay();
        SubscribeToRuntimeEventsIfReady();
        if (!initialized)
        {
            RefreshGridVisibility(gridSystemManager.Mode);
            initialized = true;
        }

        return true;
    }

    private IGridSystemProvider RequireGridSystemProvider()
    {
        return gridSystemProvider
            ?? throw new InvalidOperationException($"{nameof(GridUIManager)} requires {nameof(IGridSystemProvider)} injection.");
    }

    private IDungeonGridBuildingControllerProvider RequireBuildingControllerProvider()
    {
        return buildingControllerProvider
            ?? throw new InvalidOperationException($"{nameof(GridUIManager)} requires {nameof(IDungeonGridBuildingControllerProvider)} injection.");
    }

    private IPlayerInputReader RequireInputReader()
    {
        return inputReader
            ?? throw new InvalidOperationException($"{nameof(GridUIManager)} requires {nameof(IPlayerInputReader)} injection.");
    }

    private IUiPointerBlocker RequireUiPointerBlocker()
    {
        return uiPointerBlocker
            ?? throw new InvalidOperationException($"{nameof(GridUIManager)} requires {nameof(IUiPointerBlocker)} injection.");
    }

}

public static class GridPlacementCellAvailability
{
    public static HashSet<Vector2Int> CollectDoorInstallableCells(Grid grid, int sidePadding)
    {
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        if (grid == null)
        {
            return cells;
        }

        int safePadding = Mathf.Max(0, sidePadding);
        foreach (GridCell cell in grid.GetCells())
        {
            Vector2Int position = cell.Position;
            if (position.x < safePadding || position.x >= grid.width - safePadding)
            {
                continue;
            }

            if (GridDoorPlacementRules.TryGetTargetWall(
                grid,
                new[] { position },
                out _))
            {
                cells.Add(position);
            }
        }

        return cells;
    }

    public static HashSet<Vector2Int> CollectInstallableCells(
        Grid grid,
        BuildingSO buildingData,
        int sidePadding)
    {
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        if (grid == null || buildingData == null)
        {
            return cells;
        }

        int safePadding = Mathf.Max(0, sidePadding);
        foreach (GridCell cell in grid.GetCells())
        {
            Vector2Int position = cell.Position;
            if (position.x < safePadding || position.x >= grid.width - safePadding)
            {
                continue;
            }

            if (!cell.CanBuildInArea(buildingData)
                || !cell.CanOccupy(buildingData.Placement.Layer))
            {
                continue;
            }

            if (position.y > 0)
            {
                GridCell supportCell = grid.GetGridCell(position + Vector2Int.down);
                if (supportCell == null || !supportCell.HasPlacementSupport())
                {
                    continue;
                }
            }

            cells.Add(position);
        }

        return cells;
    }

    public static HashSet<Vector2Int> CollectInstallableCells(
        Grid grid,
        GridLayer layer,
        int sidePadding)
    {
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        if (grid == null)
        {
            return cells;
        }

        int safePadding = Mathf.Max(0, sidePadding);
        foreach (GridCell cell in grid.GetCells())
        {
            Vector2Int position = cell.Position;
            if (position.x < safePadding || position.x >= grid.width - safePadding)
            {
                continue;
            }

            if (!cell.CanOccupy(layer)
                || (layer != GridLayer.Hallway && !cell.IsBuildableArea))
            {
                continue;
            }

            if (position.y > 0)
            {
                GridCell supportCell = grid.GetGridCell(position + Vector2Int.down);
                if (supportCell == null || !supportCell.HasPlacementSupport())
                {
                    continue;
                }
            }

            cells.Add(position);
        }

        return cells;
    }
}
