using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

public enum GridMode
{
    X,
    None,
    Build,
    Destory
}

public class GridSystemManager : MonoBehaviour
{
    private readonly List<Vector2Int> mutableSelectedPositions = new List<Vector2Int>();
    private IReadOnlyList<Vector2Int> selectedPositionsView;
    private ICharacterAiWorldRegistry worldRegistry;
    public Grid grid { get; private set; }
    public GridMode Mode { get; private set; } = GridMode.None;

    [Min(1)] [Tooltip("그리드 행 개수")]
    public int defaultGridWidth;
    [Min(1)] [Tooltip("그리드 열 개수")]
    public int defaultGridHeight;
    public Vector3 gridOriginPos;
    [Header("Physical World Areas")]
    [SerializeField] private bool configureDefaultPhysicalWorldAreas = true;
    [SerializeField] private bool centerDungeonInteriorHorizontally = true;
    [SerializeField, Min(0)] private int dungeonInteriorStartX = 4;
    [SerializeField, Min(1)] private int dungeonInteriorColumnCount = 27;
    [SerializeField, Min(0)] private int dropZoneWidth = 3;
    [SerializeField] private Vector2Int entranceGridPosition = new Vector2Int(4, 0);

    public Vector2Int firstSelectedPos { get; private set; }
    public Vector2Int lastSelectedPos { get; private set; }
    public IReadOnlyList<Vector2Int> totalSelectedPos =>
        selectedPositionsView ??= ReadOnlyView.List(mutableSelectedPositions);
    public bool isDragging { get; private set; }
    public int xCount { get; private set; }
    public int yCount { get; private set; }

    public event Action OnGridExpand;
    public event Action OnGridObjectChanged;
    public event Action<GridMode> OnGridModeChanged;
    public event Action<Vector2Int> OnLastSelectedPosChanged;

    public bool CenterDungeonInteriorHorizontally => centerDungeonInteriorHorizontally;
    public int ResolvedDungeonInteriorStartX => ResolveDungeonInteriorStartX(
        grid != null ? grid.width : defaultGridWidth);
    public int AuthoredLayoutHorizontalShift => ResolvedDungeonInteriorStartX
        - Mathf.Max(0, dungeonInteriorStartX);
    public Vector2Int ResolvedEntranceGridPosition
    {
        get
        {
            int width = grid != null ? grid.width : Mathf.Max(1, defaultGridWidth);
            int resolvedX = Mathf.Clamp(
                entranceGridPosition.x + AuthoredLayoutHorizontalShift,
                0,
                Mathf.Max(0, width - 1));
            return new Vector2Int(resolvedX, entranceGridPosition.y);
        }
    }

    protected virtual void Awake()
    {
        EnsureGridInitialized();
    }

    protected virtual void OnEnable()
    {
        EnsureGridInitialized();
    }

    [Inject]
    public void Construct(ICharacterAiWorldRegistry worldRegistry)
    {
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        if (grid != null)
        {
            worldRegistry.SetGrid(grid);
        }
    }

    public void EnsureGridInitialized()
    {
        if (grid != null) return;

        grid = new Grid(defaultGridWidth, defaultGridHeight, gridOriginPos);
        ApplyDefaultPhysicalWorldAreas();
        worldRegistry?.SetGrid(grid);
    }

    protected virtual void Start()
    {
        SetGridMode(GridMode.None);
        NotifyGridObjectChanged();
    }

    public void GridExpand(int x,int y)
    {
        Grid newGrid = grid.TryExpandGrid(x,y);
        if (newGrid == null) return;

        grid = newGrid;
        ApplyDefaultPhysicalWorldAreas();
        worldRegistry?.SetGrid(grid);
        OnGridExpand?.Invoke();
    }

    public void SetGridMode(GridMode gridMode)
    {
        if (Mode == gridMode) return;

        Mode = gridMode;
        if (Mode == GridMode.None)
        {
            CancelDragSelection();
        }

        OnGridModeChanged?.Invoke(Mode);
    }

    public void SetGridModeBuild()
    {
        if (Mode == GridMode.Build) return;

        SetGridMode(GridMode.Build);
    }

    public void SetGridModeNone()
    {
        SetGridMode(GridMode.None);
    }

    public void ToggleBuildMode()
    {
        SetGridMode(Mode == GridMode.Build ? GridMode.None : GridMode.Build);
    }

    public bool TryBeginDragSelection(Vector2Int start, bool horizontalDraggable, bool verticalDraggable)
    {
        if (grid == null || !grid.IsValidGridPos(start)) return false;

        isDragging = true;
        firstSelectedPos = start;
        UpdateDragSelection(start, horizontalDraggable, verticalDraggable);
        return true;
    }

    public void UpdateDragSelection(Vector2Int pos, bool horizontalDraggable, bool verticalDraggable)
    {
        if (!isDragging && totalSelectedPos.Count == 0) return;

        lastSelectedPos = pos;
        OnLastSelectedPosChanged?.Invoke(lastSelectedPos);
        RecalculateSelectedPositions(pos, horizontalDraggable, verticalDraggable);
    }

    public List<Vector2Int> CompleteDragSelection()
    {
        isDragging = false;
        return new List<Vector2Int>(mutableSelectedPositions);
    }

    public void CancelDragSelection()
    {
        isDragging = false;
        mutableSelectedPositions.Clear();
        xCount = 0;
        yCount = 0;
    }

    public Vector3 GetWorldPosSnapped(Vector3 worldPosition)
    {
        if (grid == null) return worldPosition;

        Vector2Int pos = grid.GetXY(worldPosition);
        if (!grid.IsValidGridPos(pos)) return worldPosition;

        return grid.GetWorldPos(pos);
    }

    public void NotifyGridObjectChanged()
    {
        OnGridObjectChanged?.Invoke();
    }

    public bool TryGetEntranceGridPosition(out Vector2Int position)
    {
        EnsureGridInitialized();
        position = ResolvedEntranceGridPosition;
        if (grid == null)
        {
            return false;
        }

        if (grid.IsValidGridPos(position) && grid.IsWalkable(position))
        {
            return true;
        }

        GridCell entranceCell = grid.GetCells()
            .Where(cell => cell != null
                && cell.AreaType == GridCellAreaType.Entrance
                && grid.IsWalkable(cell.Position))
            .OrderBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .FirstOrDefault();
        if (entranceCell == null)
        {
            return false;
        }

        position = entranceCell.Position;
        return true;
    }

    private void ApplyDefaultPhysicalWorldAreas()
    {
        if (!configureDefaultPhysicalWorldAreas || grid == null)
        {
            return;
        }

        ApplyPhysicalWorldAreas(
            grid,
            ResolvedDungeonInteriorStartX,
            dungeonInteriorColumnCount,
            dropZoneWidth,
            ResolvedEntranceGridPosition);
    }

    public static int CalculateCenteredDungeonInteriorStartX(int gridWidth, int interiorColumnCount)
    {
        int width = Mathf.Max(1, gridWidth);
        int columns = Mathf.Clamp(interiorColumnCount, 1, width);
        return Mathf.CeilToInt((width - columns) * 0.5f);
    }

    private int ResolveDungeonInteriorStartX(int gridWidth)
    {
        int width = Mathf.Max(1, gridWidth);
        if (centerDungeonInteriorHorizontally)
        {
            return CalculateCenteredDungeonInteriorStartX(width, dungeonInteriorColumnCount);
        }

        return Mathf.Clamp(dungeonInteriorStartX, 0, Mathf.Max(0, width - 1));
    }

    public static void ApplyPhysicalWorldAreas(
        Grid grid,
        int dungeonInteriorStartX,
        int dungeonInteriorColumnCount,
        int dropZoneWidth,
        Vector2Int entranceGridPosition)
    {
        if (grid == null)
        {
            return;
        }

        int entranceX = Mathf.Clamp(entranceGridPosition.x, 0, grid.width - 1);
        int entranceY = Mathf.Clamp(entranceGridPosition.y, 0, grid.height - 1);
        int interiorStartX = Mathf.Clamp(dungeonInteriorStartX, 0, grid.width);
        int interiorEndX = Mathf.Clamp(
            interiorStartX + Mathf.Max(1, dungeonInteriorColumnCount),
            interiorStartX,
            grid.width);
        int dropStartX = Mathf.Max(0, entranceX - Mathf.Max(0, dropZoneWidth));

        foreach (GridCell cell in grid.GetCells())
        {
            if (cell == null)
            {
                continue;
            }

            Vector2Int pos = cell.Position;
            bool isInteriorColumn = pos.x >= interiorStartX && pos.x < interiorEndX;
            bool isExteriorGroundRow = pos.y == entranceY;
            GridCellAreaType areaType = isInteriorColumn
                ? GridCellAreaType.DungeonInterior
                : isExteriorGroundRow
                    ? GridCellAreaType.ExteriorPath
                    : GridCellAreaType.BlockedExterior;

            if (pos.y == entranceY && pos.x >= dropStartX && pos.x < entranceX)
            {
                areaType = GridCellAreaType.DropZone;
            }

            if (pos.x == entranceX && pos.y == entranceY)
            {
                areaType = GridCellAreaType.Entrance;
            }

            grid.SetAreaType(pos, areaType);
        }
    }

    private void RecalculateSelectedPositions(Vector2Int pos, bool horizontalDraggable, bool verticalDraggable)
    {
        mutableSelectedPositions.Clear();
        int startX = Mathf.Min(firstSelectedPos.x, pos.x);
        int endX = Mathf.Max(firstSelectedPos.x, pos.x);
        int startY = Mathf.Min(firstSelectedPos.y, pos.y);
        int endY = Mathf.Max(firstSelectedPos.y, pos.y);

        if (verticalDraggable && horizontalDraggable)
        {
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    mutableSelectedPositions.Add(new Vector2Int(x, y));
                }
            }
        }
        else if (verticalDraggable)
        {
            for (int y = startY; y <= endY; y++)
            {
                mutableSelectedPositions.Add(new Vector2Int(firstSelectedPos.x, y));
            }
        }
        else if (horizontalDraggable)
        {
            for (int x = startX; x <= endX; x++)
            {
                mutableSelectedPositions.Add(new Vector2Int(x, firstSelectedPos.y));
            }
        }
        else
        {
            mutableSelectedPositions.Add(firstSelectedPos);
        }

        mutableSelectedPositions.RemoveAll(gridPos => !grid.IsValidGridPos(gridPos));
        xCount = totalSelectedPos.GroupBy(gridPos => gridPos.x).Count();
        yCount = totalSelectedPos.GroupBy(gridPos => gridPos.y).Count();
    }
}
