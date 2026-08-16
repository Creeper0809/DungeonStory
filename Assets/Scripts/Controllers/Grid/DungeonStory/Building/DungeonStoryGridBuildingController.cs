using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

public class DungeonStoryGridBuildingController : MonoBehaviour
{
    [SerializeField] private GridSystemManager gridSystem;
    [SerializeField] private InputAction buildingPlaceAction;
    [SerializeField] private InputAction expandGridAction;
    public Data<BuildingSO> selectedBuilding = new Data<BuildingSO>();
    public List<InitialBuildInfo> initialPlacement = new List<InitialBuildInfo>();

    private IGridSystemProvider gridSystemProvider;
    private IDataCatalog dataCatalog;
    private IWorldPointerPositionProvider worldPointerPositionProvider;
    private IGridTextureProvider gridTextureProvider;
    private IObjectResolver objectResolver;
    private IGameSessionStateProvider gameDataProvider;
    private IGameMoneyAccount moneyAccount;
    private BlueprintResearchRuntime research;
    private IGridBuildingObjectFactory gridBuildingObjectFactory;
    private IUiPointerBlocker uiPointerBlocker;
    private IPlayerInputReader inputReader;
    private IWorkOrderRuntime workOrderRuntime;
    private IGameEventBus gameEventBus;
    private IGameClock gameClock;
    private IDungeonDebugRuleQuery debugRules;
    private IRunMilestoneQuery milestoneQuery;
    private GridBuildingPlacementService placementService;
    private bool initialized;
    private bool resetGridModeAtEndOfFrame;
    private int lastPlacementInputFrame = -1;

    public event Action<BuildingSO> OnSelectedBuildingChanged;

    public GridSystemManager GridSystem => ResolveGridSystem();
    public BuildingSO SelectedBuilding => selectedBuilding != null ? selectedBuilding.Value : null;
    public bool IsPlacementInputConsumedThisFrame => lastPlacementInputFrame == gameClock?.FrameCount;

    [Inject]
    public void Construct(
        IGridSystemProvider gridSystemProvider,
        IDataCatalog dataCatalog,
        IWorldPointerPositionProvider worldPointerPositionProvider,
        IGridTextureProvider gridTextureProvider,
        IObjectResolver objectResolver,
        IGameSessionStateProvider gameDataProvider,
        IGameMoneyAccount moneyAccount,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IGridBuildingObjectFactory gridBuildingObjectFactory,
        IUiPointerBlocker uiPointerBlocker,
        IPlayerInputReader inputReader,
        IWorkOrderRuntime workOrderRuntime,
        IGameEventBus gameEventBus,
        IGameClock gameClock,
        IDungeonDebugRuleQuery debugRules,
        IRunMilestoneQuery milestoneQuery = null)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.dataCatalog = dataCatalog
            ?? throw new ArgumentNullException(nameof(dataCatalog));
        this.worldPointerPositionProvider = worldPointerPositionProvider
            ?? throw new ArgumentNullException(nameof(worldPointerPositionProvider));
        this.gridTextureProvider = gridTextureProvider
            ?? throw new ArgumentNullException(nameof(gridTextureProvider));
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.moneyAccount = moneyAccount
            ?? throw new ArgumentNullException(nameof(moneyAccount));
        research = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(DungeonStoryGridBuildingController)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        this.gridBuildingObjectFactory = gridBuildingObjectFactory
            ?? throw new ArgumentNullException(nameof(gridBuildingObjectFactory));
        this.uiPointerBlocker = uiPointerBlocker
            ?? throw new ArgumentNullException(nameof(uiPointerBlocker));
        this.inputReader = inputReader
            ?? throw new ArgumentNullException(nameof(inputReader));
        this.workOrderRuntime = workOrderRuntime
            ?? throw new ArgumentNullException(nameof(workOrderRuntime));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.debugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
        this.milestoneQuery = milestoneQuery;
    }

    private void Awake()
    {
        if (selectedBuilding == null)
        {
            selectedBuilding = new Data<BuildingSO>();
        }

        EnsureInputActions();
    }

    private void Start()
    {
        EnsureInitialized();
        selectedBuilding.Initialize(null);
        if (gridSystem == null) return;

        gridSystem.SetGridMode(GridMode.None);
        gridSystem.NotifyGridObjectChanged();
    }

    private void EnsureInitialized()
    {
        if (initialized && placementService != null) return;

        gridSystem = ResolveGridSystem();
        if (gridSystem == null || gridSystem.grid == null) return;

        IReadOnlyDictionary<int, BuildingSO> buildingDataById = ResolveDataCatalog().GetData<BuildingSO>();
        BuildingSO hallwayBuilding = null;
        buildingDataById?.TryGetValue(0, out hallwayBuilding);
        GridBuildingFactory buildingFactory = new GridBuildingFactory(
            ResolveGridTexture(),
            ConfigurePlacedBuilding,
            ResolveGridBuildingObjectFactory());
        placementService = new GridBuildingPlacementService(
            gridSystem.grid,
            hallwayBuilding,
            FindBuildingDataById,
            buildingFactory,
            new BuildingPlacementValidator(new GridPlacementValidator(), CreateBuildingConditionContext),
            workOrderRuntime,
            ConfigurePlacedBuilding);
        if (!HasAnyPlacedStructures(gridSystem.grid))
        {
            placementService.PlaceInitialBuildings(NormalizeInitialPlacementForCurrentGrid(
                initialPlacement,
                centerAuthoredLayout: true));
        }
        gridSystem.OnGridExpand -= OnGridExpand;
        gridSystem.OnGridObjectChanged -= DrawGridTextureWalls;
        gridSystem.OnGridExpand += OnGridExpand;
        gridSystem.OnGridObjectChanged += DrawGridTextureWalls;
        initialized = true;
        DrawGridTextureWalls();
    }

    private void Update()
    {
        if (gridSystem == null || gridSystem.grid == null) return;

        if (inputReader != null
            && inputReader.GetMouseButtonDown(0)
            && lastPlacementInputFrame != gameClock.FrameCount)
        {
            TriggerPlaceBuilding();
            TriggerDestroyBuildableObject();
        }

        if (!gridSystem.isDragging || SelectedBuilding == null) return;

        gridSystem.UpdateDragSelection(
            gridSystem.grid.GetXY(ResolveMouseWorldPosition()),
            SelectedBuilding.horizontalDraggable,
            SelectedBuilding.verticalDraggable);
    }

    private void LateUpdate()
    {
        if (!resetGridModeAtEndOfFrame)
        {
            return;
        }

        resetGridModeAtEndOfFrame = false;
        if (gridSystem != null)
        {
            gridSystem.SetGridModeNone();
        }
    }

    public void TriggerPlaceBuilding()
    {
        EnsureInitialized();
        if (gridSystem == null || gridSystem.grid == null || placementService == null) return;
        if (gridSystem.Mode != GridMode.Build || SelectedBuilding == null) return;
        if (RequireUiPointerBlocker().IsPointerOverUi()) return;

        lastPlacementInputFrame = gameClock.FrameCount;

        if (!SelectedBuilding.GetDraggable())
        {
            Vector2Int pos = gridSystem.grid.GetXY(ResolveMouseWorldPosition());
            PlaceBuilding(new List<Vector2Int> { pos });
            return;
        }

        if (!gridSystem.isDragging)
        {
            Vector2Int firstSelectedPos = gridSystem.grid.GetXY(ResolveMouseWorldPosition());
            if (!gridSystem.TryBeginDragSelection(firstSelectedPos, SelectedBuilding.horizontalDraggable, SelectedBuilding.verticalDraggable))
            {
                gameEventBus.ShowNotice("유효하지 않은 위치입니다.", NoticeFeedEvent.Grade.DANGER);
            }
            return;
        }

        PlaceBuilding(gridSystem.CompleteDragSelection());
    }

    public void TriggerDestroyBuildableObject()
    {
        EnsureInitialized();
        if (gridSystem == null || gridSystem.grid == null || placementService == null) return;
        if (gridSystem.Mode != GridMode.Destory) return;
        if (RequireUiPointerBlocker().IsPointerOverUi()) return;

        BuildableObject building = GetBuildingByMousePos();
        if (!building) return;

        if (!placementService.TryDestroyBuilding(building, out BuildingSO buildingData, out string errorMessage))
        {
            gameEventBus.ShowNotice(errorMessage, NoticeFeedEvent.Grade.DANGER);
            return;
        }

        int refund = FacilityProgression.GetRefund(buildingData);
        string refundText = refund > 0 ? $" · {refund} 환급" : string.Empty;
        gameEventBus.ShowNotice($"{buildingData.objectName} 철거 완료{refundText}", NoticeFeedEvent.Grade.NONE);
        gridSystem.NotifyGridObjectChanged();
    }

    private void OnEnable()
    {
        EnsureInputActions();

        buildingPlaceAction.performed += OnBuildingPlaceInput;
        expandGridAction.performed += OnExpandGridInput;
        buildingPlaceAction.Enable();
        expandGridAction.Enable();
    }

    private void OnDisable()
    {
        if (buildingPlaceAction == null || expandGridAction == null) return;

        buildingPlaceAction.performed -= OnBuildingPlaceInput;
        expandGridAction.performed -= OnExpandGridInput;
        buildingPlaceAction.Disable();
        expandGridAction.Disable();
    }

    public BuildableObject GetBuildingByMousePos()
    {
        EnsureInitialized();
        if (gridSystem == null || gridSystem.grid == null) return null;

        Vector2Int pos = gridSystem.grid.GetXY(ResolveMouseWorldPosition());
        if (!gridSystem.grid.IsValidGridPos(pos)) return null;

        return gridSystem.grid.GetGridCell(pos).GetBuilding();
    }

    public bool IsBuildable()
    {
        EnsureInitialized();
        if (gridSystem == null || placementService == null) return false;

        return SelectedBuilding != null && placementService.CanPlaceBuilding(SelectedBuilding, gridSystem.firstSelectedPos);
    }

    public bool IsBuildableAt(Vector2Int pos)
    {
        EnsureInitialized();
        if (placementService == null) return false;

        return SelectedBuilding != null && placementService.CanPlaceBuilding(SelectedBuilding, pos);
    }

    public bool HasAnyPlacedGridOccupants()
    {
        EnsureInitialized();
        return HasAnyPlacedStructures(gridSystem != null ? gridSystem.grid : null);
    }

    public bool TryPlaceInitialBuildings(
        IEnumerable<InitialBuildInfo> placements,
        out string message)
    {
        EnsureInitialized();
        if (gridSystem == null || gridSystem.grid == null || placementService == null)
        {
            message = "그리드 배치 시스템이 준비되지 않았습니다.";
            return false;
        }

        int before = CountGridOccupants(gridSystem.grid);
        placementService.PlaceInitialBuildings(NormalizeInitialPlacementForCurrentGrid(
            placements,
            centerAuthoredLayout: false));
        int after = CountGridOccupants(gridSystem.grid);
        gridSystem.NotifyGridObjectChanged();
        message = $"초기 배치 {Mathf.Max(0, after - before)}개를 적용했습니다.";
        return after > before;
    }

    /// <summary>
    /// Command boundary for non-pointer callers such as automation and
    /// deterministic gameplay verification.  It owns the same placement,
    /// construction-order and grid-notification path as the mouse command.
    /// </summary>
    public bool TryPlaceConstructionSite(
        BuildingSO building,
        Vector2Int position,
        out string message)
    {
        EnsureInitialized();
        if (gridSystem == null || gridSystem.grid == null
            || placementService == null || building == null)
        {
            message = "Construction placement is not ready.";
            return false;
        }

        if (!placementService.TryPlaceConstructionSite(
                building,
                position,
                out message))
        {
            return false;
        }

        gridSystem.NotifyGridObjectChanged();
        return true;
    }

    /// <summary>
    /// Production destruction command for an already resolved building.  The
    /// caller never manipulates GameObject activation or grid occupants.
    /// </summary>
    public bool TryDestroyBuilding(
        BuildableObject building,
        out string message)
    {
        EnsureInitialized();
        if (gridSystem == null || gridSystem.grid == null
            || placementService == null || building == null)
        {
            message = "Building destruction is not ready.";
            return false;
        }

        if (!placementService.TryDestroyBuilding(
                building,
                out _,
                out message))
        {
            return false;
        }

        gridSystem.NotifyGridObjectChanged();
        return true;
    }

    public Vector3 GetMouseWorldPosSnapped()
    {
        EnsureInitialized();
        if (gridSystem == null) return ResolveMouseWorldPosition();

        return gridSystem.GetWorldPosSnapped(ResolveMouseWorldPosition());
    }

    public void SelectBuildingById(int id)
    {
        EnsureInitialized();
        if (gridSystem == null) return;
        if (gridSystem.Mode == GridMode.None && id != -1) return;

        BuildingSO building = null;
        if (id != -1)
        {
            building = ResolveBuildingDataById(id);
        }

        selectedBuilding.Value = building;
        OnSelectedBuildingChanged?.Invoke(building);
    }

    public void ClearBuildingSO()
    {
        SelectBuildingById(-1);
    }

    public void SetGridModeBuild()
    {
        EnsureInitialized();
        if (gridSystem == null) return;

        resetGridModeAtEndOfFrame = false;
        gridSystem.SetGridModeBuild();
    }

    public void SetGridModeNone()
    {
        EnsureInitialized();
        if (gridSystem == null) return;

        resetGridModeAtEndOfFrame = false;
        gridSystem.SetGridModeNone();
        ClearBuildingSO();
    }

    public void SetDestroyMode()
    {
        EnsureInitialized();
        if (gridSystem == null) return;

        resetGridModeAtEndOfFrame = false;
        gridSystem.SetGridMode(GridMode.Destory);
        ClearBuildingSO();
    }

    private void PlaceBuilding(List<Vector2Int> poses)
    {
        EnsureInitialized();
        if (placementService == null || SelectedBuilding == null) return;
        int placedCount = 0;
        BuildingSO building = SelectedBuilding;

        foreach (var pos in poses)
        {
            if (!placementService.TryPlaceConstructionSite(building, pos, out string errorMessage))
            {
                gameEventBus.ShowNotice(errorMessage, NoticeFeedEvent.Grade.DANGER);
                continue;
            }

            placedCount++;
            gridSystem.NotifyGridObjectChanged();
        }

        if (placedCount == 0) return;

        if (placedCount > 0)
        {
            gameEventBus.ShowNotice($"{building.objectName} x{placedCount} 공사 예약", NoticeFeedEvent.Grade.NONE);
        }

        ClearBuildingSO();
        resetGridModeAtEndOfFrame = true;
    }

    private void DrawGridTextureWalls()
    {
        ResolveGridTexture().DrawWall(gridSystem.grid);
    }

    private IEnumerable<InitialBuildInfo> NormalizeInitialPlacementForCurrentGrid(
        IEnumerable<InitialBuildInfo> placements,
        bool centerAuthoredLayout)
    {
        if (placements == null)
        {
            yield break;
        }

        Grid grid = gridSystem != null ? gridSystem.grid : null;
        int horizontalShift = centerAuthoredLayout && gridSystem != null
            ? gridSystem.AuthoredLayoutHorizontalShift
            : 0;
        foreach (InitialBuildInfo placement in placements)
        {
            if (placement == null || placement.Building == null || grid == null)
            {
                yield return placement;
                continue;
            }

            InitialBuildInfo translated = horizontalShift == 0
                ? placement
                : new InitialBuildInfo
                {
                    Position = placement.Position + new Vector2Int(horizontalShift, 0),
                    Building = placement.Building
                };
            if (ShouldKeepInitialPlacementPosition(translated.Building)
                || IsInitialPlacementValidForCurrentAreas(grid, translated, translated.Position))
            {
                yield return translated;
                continue;
            }

            Vector2Int shifted = FindNearestInteriorInitialPlacement(grid, translated, translated.Position);
            yield return new InitialBuildInfo
            {
                Position = shifted,
                Building = translated.Building
            };
        }
    }

    private static bool ShouldKeepInitialPlacementPosition(BuildingSO building)
    {
        return building == null
            || building.Placement.Layer == GridLayer.Hallway
            || (building.IsDoor && !building.IsInteriorDoor);
    }

    private Vector2Int FindNearestInteriorInitialPlacement(Grid grid, InitialBuildInfo placement, Vector2Int original)
    {
        if (grid == null || placement?.Building == null)
        {
            return original;
        }

        if (IsInitialPlacementValidForCurrentAreas(grid, placement, original))
        {
            return original;
        }

        for (int offset = 1; offset < grid.width; offset++)
        {
            Vector2Int candidate = original + new Vector2Int(offset, 0);
            if (IsInitialPlacementValidForCurrentAreas(grid, placement, candidate))
            {
                return candidate;
            }
        }

        return original;
    }

    private bool IsInitialPlacementValidForCurrentAreas(Grid grid, InitialBuildInfo placement, Vector2Int position)
    {
        if (grid == null || placement?.Building == null)
        {
            return false;
        }

        foreach (InitialBuildInfo effectivePlacement in GetEffectiveInitialPlacementAreaEntries(placement, position))
        {
            if (effectivePlacement?.Building == null)
            {
                return false;
            }

            foreach (Vector2Int gridPos in effectivePlacement.Building.GetGridPosList(effectivePlacement.Position))
            {
                GridCell cell = grid.GetGridCell(gridPos);
                if (cell == null || !cell.CanBuildInArea(effectivePlacement.Building))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private IEnumerable<InitialBuildInfo> GetEffectiveInitialPlacementAreaEntries(
        InitialBuildInfo placement,
        Vector2Int position)
    {
        if (placement?.Building == null)
        {
            yield break;
        }

        InitialBuildInfo relocated = new InitialBuildInfo
        {
            Position = position,
            Building = placement.Building
        };

        if (!ModularFacilityInitialPlacementMigrator.TryExpand(
                relocated,
                FindBuildingDataById,
                out IReadOnlyList<InitialBuildInfo> expanded))
        {
            yield return relocated;
            yield break;
        }

        BuildingSO roomBoundary = FindBuildingDataById(7);
        if (roomBoundary != null)
        {
            int startX = position.x - (placement.Building.width / 2);
            int endX = startX + Mathf.Max(1, placement.Building.width) - 1;
            yield return new InitialBuildInfo
            {
                Position = new Vector2Int(startX - 1, position.y),
                Building = roomBoundary
            };
            yield return new InitialBuildInfo
            {
                Position = new Vector2Int(endX + 1, position.y),
                Building = roomBoundary
            };
        }

        foreach (InitialBuildInfo item in expanded)
        {
            if (item != null)
            {
                yield return item;
            }
        }
    }

    private static bool HasAnyGridOccupants(Grid grid)
    {
        if (grid == null) return false;

        foreach (GridCell cell in grid.GetCells())
        {
            if (cell != null && cell.HasOccupant())
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnyPlacedStructures(Grid grid)
    {
        if (grid == null) return false;

        foreach (GridCell cell in grid.GetCells())
        {
            if (cell == null)
            {
                continue;
            }

            if (cell.HasOccupantInLayer(GridLayer.Hallway)
                || cell.HasOccupantInLayer(GridLayer.Building))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountGridOccupants(Grid grid)
    {
        if (grid == null) return 0;

        int count = 0;
        foreach (GridCell cell in grid.GetCells())
        {
            if (cell != null && cell.HasOccupant())
            {
                count++;
            }
        }

        return count;
    }

    private void OnGridExpand()
    {
        if (placementService == null || gridSystem == null) return;

        placementService.SetGrid(gridSystem.grid);
        DrawGridTextureWalls();
    }

    private void OnDestroy()
    {
        if (gridSystem != null)
        {
            gridSystem.OnGridExpand -= OnGridExpand;
            gridSystem.OnGridObjectChanged -= DrawGridTextureWalls;
        }

    }

    private void EnsureInputActions()
    {
        buildingPlaceAction ??= new InputAction("Building Place", binding: "<Mouse>/leftButton");
        expandGridAction ??= new InputAction("Expand Grid", binding: "<Keyboard>/e");

        if (buildingPlaceAction.bindings.Count == 0)
        {
            buildingPlaceAction.AddBinding("<Mouse>/leftButton");
        }

        if (expandGridAction.bindings.Count == 0)
        {
            expandGridAction.AddBinding("<Keyboard>/e");
        }
    }

    private void OnBuildingPlaceInput(InputAction.CallbackContext context)
    {
        TriggerPlaceBuilding();
        TriggerDestroyBuildableObject();
    }

    private void OnExpandGridInput(InputAction.CallbackContext context)
    {
        EnsureInitialized();
        if (gridSystem == null) return;

        gridSystem.GridExpand(2, 2);
    }

    private BuildingSO FindBuildingDataById(int id)
    {
        return TryResolveBuildingDataById(id, out BuildingSO buildingData)
            ? buildingData
            : null;
    }

    private BuildingConditionContext CreateBuildingConditionContext()
    {
        ResolveGameDataProvider().TryGetSessionState(out GameSessionState gameData);
        BlueprintResearchState researchState = research.State;
        return new BuildingConditionContext(
            gameData,
            researchState,
            moneyAccount,
            debugRules,
            milestoneQuery);
    }

    private void ConfigurePlacedBuilding(BuildableObject building)
    {
        if (building == null) return;

        ResolveObjectResolver().Inject(building);
        building.OnBuildingClicked -= OnPlacedBuildingClicked;
        building.OnBuildingClicked += OnPlacedBuildingClicked;
    }

    private void OnPlacedBuildingClicked(BuildableObject building)
    {
        if (building == null) return;
        if (IsPlacementInputConsumedThisFrame) return;
        if (gridSystem != null && gridSystem.Mode != GridMode.None) return;
        if (RequireUiPointerBlocker().IsPointerOverUi()) return;

        gameEventBus.ShowInfo(new BuildingInfoTarget(building));
    }

    private GridSystemManager ResolveGridSystem()
    {
        if (gridSystem != null)
        {
            return gridSystem;
        }

        gridSystem = ResolveGridSystemProvider().Manager;
        return gridSystem;
    }

    private IGridSystemProvider ResolveGridSystemProvider()
    {
        return gridSystemProvider
            ?? throw new InvalidOperationException($"{nameof(DungeonStoryGridBuildingController)} requires {nameof(IGridSystemProvider)} injection.");
    }

    private IDataCatalog ResolveDataCatalog()
    {
        return dataCatalog
            ?? throw new InvalidOperationException($"{nameof(DungeonStoryGridBuildingController)} requires {nameof(IDataCatalog)} injection.");
    }

    private Vector3 ResolveMouseWorldPosition()
    {
        IWorldPointerPositionProvider provider = worldPointerPositionProvider
            ?? throw new InvalidOperationException($"{nameof(DungeonStoryGridBuildingController)} requires {nameof(IWorldPointerPositionProvider)} injection.");
        return provider.MouseWorldPosition;
    }

    private GridTexture ResolveGridTexture()
    {
        IGridTextureProvider provider = gridTextureProvider
            ?? throw new InvalidOperationException($"{nameof(DungeonStoryGridBuildingController)} requires {nameof(IGridTextureProvider)} injection.");
        return provider.Texture;
    }

    private IObjectResolver ResolveObjectResolver()
    {
        return objectResolver
            ?? throw new InvalidOperationException($"{nameof(DungeonStoryGridBuildingController)} requires {nameof(IObjectResolver)} injection.");
    }

    private IGameSessionStateProvider ResolveGameDataProvider()
    {
        return gameDataProvider
            ?? throw new InvalidOperationException($"{nameof(DungeonStoryGridBuildingController)} requires {nameof(IGameSessionStateProvider)} injection.");
    }

    private IGridBuildingObjectFactory ResolveGridBuildingObjectFactory()
    {
        return gridBuildingObjectFactory
            ?? throw new InvalidOperationException($"{nameof(DungeonStoryGridBuildingController)} requires {nameof(IGridBuildingObjectFactory)} injection.");
    }

    private IUiPointerBlocker RequireUiPointerBlocker()
    {
        return uiPointerBlocker
            ?? throw new InvalidOperationException($"{nameof(DungeonStoryGridBuildingController)} requires {nameof(IUiPointerBlocker)} injection.");
    }

    private BuildingSO ResolveBuildingDataById(int id)
    {
        if (TryResolveBuildingDataById(id, out BuildingSO buildingData))
        {
            return buildingData;
        }

        throw new KeyNotFoundException($"BuildingSO id {id} was not found in {nameof(IDataCatalog)}.");
    }

    private bool TryResolveBuildingDataById(int id, out BuildingSO buildingData)
    {
        IReadOnlyDictionary<int, BuildingSO> buildingDataById = ResolveDataCatalog().GetData<BuildingSO>();
        return buildingDataById.TryGetValue(id, out buildingData);
    }
}
