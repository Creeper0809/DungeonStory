using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class DungeonStoryGridGhostPresenter : GridPlacementGhostPresenter
{
    private IGridSystemProvider gridSystemProvider;
    private IDungeonGridBuildingControllerProvider buildingControllerProvider;
    private IWorldPointerPositionProvider worldPointerPositionProvider;
    private GridSystemManager gridSystemManager;
    private DungeonStoryGridBuildingController buildingController;
    private bool subscribed;

    protected override Grid ActiveGrid => TryGetGridSystem(out GridSystemManager manager)
        ? manager.grid
        : null;
    protected override bool HasGhostSelection => TryGetSelectedBuilding(out _)
        && TryGetGridSystem(out GridSystemManager manager)
        && manager.Mode == GridMode.Build;
    protected override Sprite GhostSprite
    {
        get
        {
            if (!TryGetSelectedBuilding(out BuildingSO building))
            {
                return null;
            }

            return building.sprite != null ? building.sprite : building.icon;
        }
    }
    protected override int GhostWidth => TryGetSelectedBuilding(out BuildingSO building)
        ? building.width
        : 1;
    protected override int GhostHeight => TryGetSelectedBuilding(out BuildingSO building)
        ? building.height
        : 1;
    protected override bool GhostUsesFullFootprint
    {
        get
        {
            if (!TryGetSelectedBuilding(out BuildingSO building))
            {
                return false;
            }

            return building.IsStructuralWall
                || (building.tiles != null && building.tiles.Count > 0);
        }
    }
    protected override Vector2 GhostVisualOffset => TryGetSelectedBuilding(out BuildingSO building) && building.IsInteriorDoor
        ? new Vector2(0f, InteriorDoorVisualLayout.GroundingOffsetY)
        : Vector2.zero;
    protected override Vector2? GhostVisualSizeOverride => TryGetSelectedBuilding(out BuildingSO building) && building.IsInteriorDoor
        ? new Vector2(InteriorDoorVisualLayout.VisualWidth, InteriorDoorVisualLayout.VisualHeight)
        : null;
    protected override float GridOriginZ => TryGetGridSystem(out GridSystemManager manager)
        ? manager.gridOriginPos.z
        : 0f;
    protected override bool IsDraggingSelection => TryGetGridSystem(out GridSystemManager manager)
        && manager.isDragging;
    protected override IReadOnlyList<Vector2Int> SelectedGridPositions => TryGetGridSystem(out GridSystemManager manager)
        ? manager.totalSelectedPos
        : Array.Empty<Vector2Int>();
    protected override Vector3 MouseWorldPosition => worldPointerPositionProvider != null
        ? worldPointerPositionProvider.MouseWorldPosition
        : Vector3.zero;
    protected override Vector3 MouseWorldPositionSnapped => TryGetBuildingController(out DungeonStoryGridBuildingController controller)
        ? controller.GetMouseWorldPosSnapped()
        : MouseWorldPosition;

    [Inject]
    public void Construct(
        IGridSystemProvider gridSystemProvider,
        IDungeonGridBuildingControllerProvider buildingControllerProvider,
        IWorldPointerPositionProvider worldPointerPositionProvider,
        IGridGhostObjectResolver ghostObjectResolver)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.buildingControllerProvider = buildingControllerProvider
            ?? throw new ArgumentNullException(nameof(buildingControllerProvider));
        this.worldPointerPositionProvider = worldPointerPositionProvider
            ?? throw new ArgumentNullException(nameof(worldPointerPositionProvider));
        ConstructGridPlacementGhostPresenter(ghostObjectResolver);
        SubscribeToRuntimeEventsIfInjected();
    }

    protected override void Awake()
    {
        base.Awake();
    }

    protected override bool CanPlaceAt(Vector2Int gridPosition)
    {
        return TryGetBuildingController(out DungeonStoryGridBuildingController controller)
            && controller.IsBuildableAt(gridPosition);
    }

    private void OnGridModeChanged(GridMode gridMode)
    {
        if (gridMode == GridMode.None)
        {
            HideGhost();
            return;
        }

        RefreshGhostSelection();
    }

    public void OnSelectedChanged(BuildingSO buildingSO)
    {
        RefreshGhostSelection();
    }

    private void OnEnable()
    {
        SubscribeToRuntimeEventsIfInjected();
    }

    private void Start()
    {
        SubscribeToRuntimeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromRuntimeEvents();
    }

    private void SubscribeToRuntimeEventsIfInjected()
    {
        if (gridSystemProvider != null && buildingControllerProvider != null && isActiveAndEnabled)
        {
            SubscribeToRuntimeEvents();
        }
    }

    private void SubscribeToRuntimeEvents()
    {
        if (!TryGetGridSystem(out GridSystemManager manager)
            || !TryGetBuildingController(out DungeonStoryGridBuildingController controller))
        {
            return;
        }

        if (subscribed && gridSystemManager == manager && buildingController == controller)
        {
            return;
        }

        UnsubscribeFromRuntimeEvents();
        gridSystemManager = manager;
        buildingController = controller;
        buildingController.OnSelectedBuildingChanged += OnSelectedChanged;
        gridSystemManager.OnGridModeChanged += OnGridModeChanged;
        subscribed = true;
    }

    private void UnsubscribeFromRuntimeEvents()
    {
        if (!subscribed)
        {
            return;
        }

        buildingController.OnSelectedBuildingChanged -= OnSelectedChanged;
        gridSystemManager.OnGridModeChanged -= OnGridModeChanged;
        subscribed = false;
        buildingController = null;
        gridSystemManager = null;
    }

    private IGridSystemProvider RequireGridSystemProvider()
    {
        return gridSystemProvider
            ?? throw new InvalidOperationException($"{nameof(DungeonStoryGridGhostPresenter)} requires {nameof(IGridSystemProvider)} injection.");
    }

    private GridSystemManager RequireGridSystem()
    {
        return gridSystemManager ?? RequireGridSystemProvider().Manager;
    }

    private IDungeonGridBuildingControllerProvider RequireBuildingControllerProvider()
    {
        return buildingControllerProvider
            ?? throw new InvalidOperationException($"{nameof(DungeonStoryGridGhostPresenter)} requires {nameof(IDungeonGridBuildingControllerProvider)} injection.");
    }

    private DungeonStoryGridBuildingController RequireBuildingController()
    {
        return buildingController ?? RequireBuildingControllerProvider().Controller;
    }

    private IWorldPointerPositionProvider RequireWorldPointerPositionProvider()
    {
        return worldPointerPositionProvider
            ?? throw new InvalidOperationException($"{nameof(DungeonStoryGridGhostPresenter)} requires {nameof(IWorldPointerPositionProvider)} injection.");
    }

    private bool TryGetGridSystem(out GridSystemManager manager)
    {
        manager = gridSystemManager;
        if (manager == null && gridSystemProvider != null)
        {
            manager = gridSystemProvider.Manager;
        }

        return manager != null && manager.grid != null;
    }

    private bool TryGetBuildingController(out DungeonStoryGridBuildingController controller)
    {
        controller = buildingController;
        if (controller == null && buildingControllerProvider != null)
        {
            controller = buildingControllerProvider.Controller;
        }

        return controller != null;
    }

    private bool TryGetSelectedBuilding(out BuildingSO building)
    {
        building = null;
        if (!TryGetBuildingController(out DungeonStoryGridBuildingController controller))
        {
            return false;
        }

        building = controller.SelectedBuilding;
        return building != null;
    }
}
