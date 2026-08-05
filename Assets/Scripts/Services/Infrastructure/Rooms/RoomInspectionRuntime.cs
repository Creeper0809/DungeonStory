using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

public interface IRoomInspectionService
{
    bool IsEnabled { get; }
    RoomEnvironmentSnapshot CurrentSnapshot { get; }
    int OverlayCellCount { get; }
    Button ToggleButton { get; }
    GameObject PanelObject { get; }
    void Toggle();
    void SetEnabled(bool enabled);
    bool ShowRoom(Grid grid, RoomInstance room);
}

public interface IRoomInspectionInteractionContext
{
    float Time { get; }
    bool IsPointerOverUi();
    bool TryGetHoveredCell(Grid grid, out Vector2Int cell);
}

public sealed class RoomInspectionInteractionContext :
    IRoomInspectionInteractionContext
{
    private readonly IMainCameraProvider mainCameraProvider;
    private readonly IPlayerInputReader inputReader;
    private readonly IUiPointerBlocker uiPointerBlocker;
    private readonly IUiClock uiClock;

    public RoomInspectionInteractionContext(
        IMainCameraProvider mainCameraProvider,
        IPlayerInputReader inputReader,
        IUiPointerBlocker uiPointerBlocker,
        IUiClock uiClock)
    {
        this.mainCameraProvider = mainCameraProvider
            ?? throw new ArgumentNullException(nameof(mainCameraProvider));
        this.inputReader = inputReader
            ?? throw new ArgumentNullException(nameof(inputReader));
        this.uiPointerBlocker = uiPointerBlocker
            ?? throw new ArgumentNullException(nameof(uiPointerBlocker));
        this.uiClock = uiClock
            ?? throw new ArgumentNullException(nameof(uiClock));
    }

    public float Time => uiClock.Time;

    public bool IsPointerOverUi() => uiPointerBlocker.IsPointerOverUi();

    public bool TryGetHoveredCell(Grid grid, out Vector2Int cell)
    {
        cell = default;
        Camera camera = mainCameraProvider.Camera;
        if (grid == null || camera == null)
        {
            return false;
        }

        Vector3 screenPosition = inputReader.MousePosition;
        screenPosition.z = -camera.transform.position.z;
        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        cell = grid.GetXY(worldPosition);
        return grid.IsValidGridPos(cell);
    }
}

public sealed class RoomInspectionRuntime :
    IRoomInspectionService,
    IStartable,
    ITickable,
    IDisposable
{
    private const float DynamicRefreshInterval = 0.25f;

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly IRoomEnvironmentEvaluator evaluator;
    private readonly IRoomEnvironmentSettingsProvider settingsProvider;
    private readonly IRoomInspectionInteractionContext interactionContext;
    private readonly IDungeonUiCanvasProvider canvasProvider;
    private readonly ITmpKoreanFontService fontService;

    private GridSystemManager gridSystemManager;
    private RoomInspectionView view;
    private RoomOverlayPresenter overlay;
    private RoomInstance currentRoom;
    private int currentGridVersion = -1;
    private float nextDynamicRefreshAt;
    private bool started;

    public RoomInspectionRuntime(
        IGridSystemProvider gridSystemProvider,
        IRoomLayoutCache roomLayoutCache,
        IRoomEnvironmentEvaluator evaluator,
        IRoomEnvironmentSettingsProvider settingsProvider,
        IRoomInspectionInteractionContext interactionContext,
        IDungeonUiCanvasProvider canvasProvider,
        ITmpKoreanFontService fontService)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.evaluator = evaluator
            ?? throw new ArgumentNullException(nameof(evaluator));
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.interactionContext = interactionContext
            ?? throw new ArgumentNullException(nameof(interactionContext));
        this.canvasProvider = canvasProvider
            ?? throw new ArgumentNullException(nameof(canvasProvider));
        this.fontService = fontService
            ?? throw new ArgumentNullException(nameof(fontService));
    }

    public bool IsEnabled { get; private set; }
    public RoomEnvironmentSnapshot CurrentSnapshot { get; private set; }
    public int OverlayCellCount => overlay?.ActiveCellCount ?? 0;
    public Button ToggleButton => view?.ToggleButton;
    public GameObject PanelObject => view?.PanelObject;

    public void Start()
    {
        if (started) return;

        gridSystemManager = gridSystemProvider.Manager;
        Canvas canvas = canvasProvider.GetOrCreateCanvas()
            ?? throw new InvalidOperationException("Room inspection requires the main dungeon Canvas.");
        RectTransform upperRightPanel = canvas.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault((item) => item != null && item.name == "UpperRightPanel")
            ?? throw new InvalidOperationException("Room inspection requires UpperRightPanel under the main Canvas.");

        view = new RoomInspectionView(
            canvas,
            upperRightPanel,
            fontService,
            Toggle);
        overlay = new RoomOverlayPresenter();
        gridSystemManager.OnGridModeChanged += OnGridModeChanged;
        view.SetToggleInteractable(gridSystemManager.Mode == GridMode.None);
        view.SetToggleState(false);
        started = true;
    }

    public void Tick()
    {
        if (!started || !IsEnabled)
        {
            return;
        }

        if (gridSystemManager.Mode != GridMode.None)
        {
            SetEnabled(false);
            return;
        }

        if (interactionContext.IsPointerOverUi())
        {
            return;
        }

        Grid grid = gridSystemProvider.Grid;
        if (!interactionContext.TryGetHoveredCell(grid, out Vector2Int cell))
        {
            ClearRoom();
            return;
        }

        if (!roomLayoutCache.TryGetRoom(grid, cell, out RoomInstance room)
            || room == null
            || room.IsSelfContained)
        {
            ClearRoom();
            return;
        }

        bool roomChanged = !ReferenceEquals(currentRoom, room)
            || currentGridVersion != grid.StructuralVersion;
        if (!roomChanged && interactionContext.Time < nextDynamicRefreshAt)
        {
            return;
        }

        ShowRoomInternal(grid, room);
    }

    public void Toggle()
    {
        SetEnabled(!IsEnabled);
    }

    public void SetEnabled(bool enabled)
    {
        EnsureStarted();
        bool canEnable = enabled && gridSystemManager.Mode == GridMode.None;
        IsEnabled = canEnable;
        view.SetToggleState(canEnable);
        if (!canEnable)
        {
            ClearRoom();
        }
    }

    public bool ShowRoom(Grid grid, RoomInstance room)
    {
        EnsureStarted();
        if (grid == null
            || room == null
            || room.IsSelfContained
            || gridSystemManager.Mode != GridMode.None)
        {
            return false;
        }

        IsEnabled = true;
        view.SetToggleState(true);
        ShowRoomInternal(grid, room);
        return true;
    }

    public void Dispose()
    {
        if (gridSystemManager != null)
        {
            gridSystemManager.OnGridModeChanged -= OnGridModeChanged;
        }

        overlay?.Dispose();
        view?.Dispose();
        overlay = null;
        view = null;
        started = false;
    }

    private void OnGridModeChanged(GridMode mode)
    {
        view?.SetToggleInteractable(mode == GridMode.None);
        if (mode != GridMode.None)
        {
            SetEnabled(false);
        }
    }

    private void ShowRoomInternal(Grid grid, RoomInstance room)
    {
        CurrentSnapshot = evaluator.Evaluate(grid, room);
        currentRoom = room;
        currentGridVersion = grid.StructuralVersion;
        nextDynamicRefreshAt = interactionContext.Time + DynamicRefreshInterval;
        Color roomColor = ResolveRoomColor(CurrentSnapshot);
        overlay.Show(CurrentSnapshot, roomColor);
        view.Render(CurrentSnapshot, roomColor);
    }

    private Color ResolveRoomColor(RoomEnvironmentSnapshot snapshot)
    {
        if (snapshot.Status == RoomEnvironmentStatus.OpenBoundary)
        {
            return DungeonUiTheme.Danger;
        }

        if (snapshot.Status == RoomEnvironmentStatus.MissingDoor)
        {
            return DungeonUiTheme.Warning;
        }

        return settingsProvider.Settings.GetRoleColor(
            snapshot.PrimaryRole,
            snapshot.UsesMixedColor);
    }

    private void ClearRoom()
    {
        CurrentSnapshot = null;
        currentRoom = null;
        currentGridVersion = -1;
        overlay?.Clear();
        view?.Clear();
    }

    private void EnsureStarted()
    {
        if (!started)
        {
            Start();
        }
    }
}

