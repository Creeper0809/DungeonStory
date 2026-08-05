using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class InitialBuildInfo
{
    public Vector2Int Position;
    public BuildingSO Building;
}

public static class GridDoorPlacementRules
{
    public static bool TryGetTargetWall(
        Grid grid,
        IReadOnlyList<Vector2Int> positions,
        out BuildableObject wall)
    {
        wall = null;
        if (grid == null || positions == null || positions.Count != 1)
        {
            return false;
        }

        GridCell cell = grid.GetGridCell(positions[0]);
        wall = cell?.GetOccupant(GridLayer.Building) as BuildableObject;
        return wall != null
            && wall.BuildingData != null
            && wall.BuildingData.IsStructuralWall;
    }
}

public class GridBuildingPlacementService
{
    private Grid grid;
    private readonly BuildingSO hallwayBuilding;
    private readonly Func<int, BuildingSO> findBuildingData;
    private readonly IGridBuildingFactory buildingFactory;
    private readonly BuildingPlacementValidator placementValidator;
    private readonly IWorkOrderRuntime workOrderRuntime;
    private readonly Action<BuildableObject> onConstructionSiteCreated;

    public GridBuildingPlacementService(Grid grid, BuildingSO hallwayBuilding)
        : this(grid, hallwayBuilding, null)
    {
    }

    public GridBuildingPlacementService(Grid grid, BuildingSO hallwayBuilding, Func<int, BuildingSO> findBuildingData)
        : this(
            grid,
            hallwayBuilding,
            findBuildingData,
            new GridBuildingFactory(new GridBuildingObjectFactory()),
            new BuildingPlacementValidator(),
            null)
    {
    }

    public GridBuildingPlacementService(
        Grid grid,
        BuildingSO hallwayBuilding,
        Func<int, BuildingSO> findBuildingData,
        IGridBuildingFactory buildingFactory,
        BuildingPlacementValidator placementValidator,
        IWorkOrderRuntime workOrderRuntime,
        Action<BuildableObject> onConstructionSiteCreated = null)
    {
        this.grid = grid;
        this.hallwayBuilding = hallwayBuilding;
        this.findBuildingData = findBuildingData;
        this.buildingFactory = buildingFactory
            ?? throw new ArgumentNullException(nameof(buildingFactory));
        this.placementValidator = placementValidator ?? new BuildingPlacementValidator();
        this.workOrderRuntime = workOrderRuntime;
        this.onConstructionSiteCreated = onConstructionSiteCreated;
    }

    public void SetGrid(Grid grid)
    {
        this.grid = grid;
    }

    public bool TryPlaceConstructionSite(BuildingSO buildingData, Vector2Int position, out string errorMessage)
    {
        if (!CanPlaceBuilding(buildingData, position, out errorMessage))
        {
            return false;
        }

        if (workOrderRuntime == null)
        {
            return TryPlaceBuildingImmediate(buildingData, position, out errorMessage);
        }

        EnsureHallwayUnderBuildingFootprint(buildingData, position);
        if (!CreateConstructionSite(buildingData, position, out ConstructionSite site, out errorMessage))
        {
            return false;
        }

        if (!workOrderRuntime.TryCreateConstructionOrder(
                site,
                buildingData,
                position,
                out string orderId,
                out string orderFailure))
        {
            RemoveConstructionSite(site);
            errorMessage = string.IsNullOrWhiteSpace(orderFailure)
                ? "공사 주문을 만들 수 없습니다."
                : orderFailure;
            return false;
        }

        site.ConfigureSite(
            orderId,
            () => TryPlaceBuildingImmediateUnchecked(buildingData, position, chargeCost: false, out _),
            () => RemoveConstructionSite(site));
        placementValidator.ApplyBuildSuccess(buildingData);
        errorMessage = string.Empty;
        return true;
    }

    public bool TryPlaceBuilding(BuildingSO buildingData, Vector2Int position, out string errorMessage)
    {
        return TryPlaceBuildingImmediate(buildingData, position, out errorMessage);
    }

    public bool TryPlaceBuildingImmediate(BuildingSO buildingData, Vector2Int position, out string errorMessage)
    {
        if (!CanPlaceBuilding(buildingData, position, out errorMessage))
        {
            return false;
        }

        return TryPlaceBuildingImmediateUnchecked(buildingData, position, chargeCost: true, out errorMessage);
    }

    public bool TryPlaceBuildingImmediateUnchecked(
        BuildingSO buildingData,
        Vector2Int position,
        bool chargeCost,
        out string errorMessage)
    {
        if (grid == null || buildingData == null)
        {
            errorMessage = "그리드 또는 건물 데이터가 없습니다.";
            return false;
        }

        BuildableObject replacedWall = null;
        if (buildingData.IsInteriorDoor)
        {
            List<Vector2Int> doorPositions = buildingData.GetGridPosList(position);
            if (!GridDoorPlacementRules.TryGetTargetWall(grid, doorPositions, out replacedWall)
                || !grid.RemoveOccupant(
                    replacedWall,
                    replacedWall.BuildingData.Placement.Layer,
                    replacedWall.buildPoses,
                    replacedWall.BuildingData.Placement.IsMovement))
            {
                errorMessage = "문은 설치된 내벽 한 칸에만 설치할 수 있습니다.";
                return false;
            }
        }

        EnsureHallwayUnderBuildingFootprint(buildingData, position);

        if (!PlaceBuildingWithoutValidation(buildingData, position, out errorMessage))
        {
            RestoreReplacedWall(replacedWall, ref errorMessage);
            return false;
        }

        if (replacedWall != null)
        {
            buildingFactory.DeleteVisual(replacedWall.BuildingData, replacedWall.centerPos);
            replacedWall.DestroySelf();
        }

        if (chargeCost)
        {
            placementValidator.ApplyBuildSuccess(buildingData);
        }

        return true;
    }

    public bool CanPlaceBuilding(BuildingSO buildingData, Vector2Int position)
    {
        return CanPlaceBuilding(buildingData, position, out _);
    }

    public bool CanPlaceBuilding(BuildingSO buildingData, Vector2Int position, out string errorMessage)
    {
        if (placementValidator.DebugRules.IsEnabled(DungeonDebugCheat.IgnorePlacementRules))
        {
            if (grid == null || buildingData == null)
            {
                errorMessage = "그리드 또는 건물 데이터가 없습니다.";
                return false;
            }

            bool insideGrid = buildingData.GetGridPosList(position).All(grid.IsValidGridPos);
            errorMessage = insideGrid ? string.Empty : "그리드 바깥에는 설치할 수 없습니다.";
            return insideGrid;
        }

        return placementValidator.CanBuild(grid, buildingData, position, out errorMessage);
    }

    public bool TryDestroyBuilding(BuildableObject building, out BuildingSO buildingData, out string errorMessage)
    {
        buildingData = null;

        if (grid == null)
        {
            errorMessage = "그리드가 초기화되지 않았습니다";
            return false;
        }

        if (building == null)
        {
            errorMessage = "삭제할 건물이 없습니다";
            return false;
        }

        buildingData = findBuildingData?.Invoke(building.id);
        if (buildingData == null)
        {
            errorMessage = "건물 데이터를 찾을 수 없습니다";
            return false;
        }

        if (!placementValidator.CanDestroy(grid, buildingData, building, out errorMessage))
        {
            return false;
        }

        grid.RemoveOccupant(
            building,
            buildingData.Placement.Layer,
            building.buildPoses,
            buildingData.Placement.IsMovement);
        buildingFactory.DeleteVisual(buildingData, building.centerPos);
        building.DestroySelf();
        placementValidator.ApplyDestroySuccess(buildingData);
        return true;
    }

    public void PlaceInitialBuildings(IEnumerable<InitialBuildInfo> initialPlacement)
    {
        if (initialPlacement == null) return;

        List<InitialBuildInfo> placements = CollapseAdjacentRoomBoundaries(
                ModularFacilityInitialPlacementMigrator.ExpandInitialRooms(initialPlacement, findBuildingData))
            .Where((item) => item != null && item.Building != null)
            .ToList();
        foreach (InitialBuildInfo item in placements)
        {
            if (IsDuplicateInitialHallway(item))
            {
                continue;
            }

            if (!CanRegisterBuilding(item.Building, item.Position))
            {
                continue;
            }

            EnsureHallwayUnderBuildingFootprint(item.Building, item.Position);
            PlaceBuildingWithoutValidation(item.Building, item.Position, out _);
        }
    }

    private static IEnumerable<InitialBuildInfo> CollapseAdjacentRoomBoundaries(IEnumerable<InitialBuildInfo> placements)
    {
        List<InitialBuildInfo> placementList = placements?
            .Where((item) => item != null)
            .ToList()
            ?? new List<InitialBuildInfo>();
        if (placementList.Count <= 1)
        {
            return placementList;
        }

        placementList = DeduplicateSameCellRoomBoundaries(placementList);

        Dictionary<Vector2Int, int> boundaryByCell = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < placementList.Count; i++)
        {
            InitialBuildInfo item = placementList[i];
            if (IsSingleCellRoomBoundary(item.Building))
            {
                boundaryByCell[item.Position] = i;
            }
        }

        bool[] remove = new bool[placementList.Count];
        foreach (KeyValuePair<Vector2Int, int> pair in boundaryByCell)
        {
            Vector2Int right = pair.Key + Vector2Int.right;
            if (!boundaryByCell.TryGetValue(right, out int rightIndex))
            {
                continue;
            }

            int leftIndex = pair.Value;
            if (remove[leftIndex] || remove[rightIndex])
            {
                continue;
            }

            remove[ShouldRemoveRightDuplicateBoundary(placementList[leftIndex], placementList[rightIndex])
                ? rightIndex
                : leftIndex] = true;
        }

        List<InitialBuildInfo> result = new List<InitialBuildInfo>(placementList.Count);
        for (int i = 0; i < placementList.Count; i++)
        {
            if (!remove[i])
            {
                result.Add(placementList[i]);
            }
        }

        return result;
    }

    private static List<InitialBuildInfo> DeduplicateSameCellRoomBoundaries(List<InitialBuildInfo> placements)
    {
        Dictionary<Vector2Int, int> boundaryByCell = new Dictionary<Vector2Int, int>();
        bool[] remove = new bool[placements.Count];
        for (int i = 0; i < placements.Count; i++)
        {
            InitialBuildInfo item = placements[i];
            if (!IsSingleCellRoomBoundary(item?.Building))
            {
                continue;
            }

            if (!boundaryByCell.TryGetValue(item.Position, out int existingIndex))
            {
                boundaryByCell[item.Position] = i;
                continue;
            }

            InitialBuildInfo existing = placements[existingIndex];
            bool existingIsDoor = existing?.Building != null && existing.Building.IsInteriorDoor;
            bool currentIsDoor = item.Building.IsInteriorDoor;
            if (currentIsDoor && !existingIsDoor)
            {
                remove[existingIndex] = true;
                boundaryByCell[item.Position] = i;
            }
            else
            {
                remove[i] = true;
            }
        }

        List<InitialBuildInfo> result = new List<InitialBuildInfo>(placements.Count);
        for (int i = 0; i < placements.Count; i++)
        {
            if (!remove[i])
            {
                result.Add(placements[i]);
            }
        }

        return result;
    }

    private static bool ShouldRemoveRightDuplicateBoundary(InitialBuildInfo left, InitialBuildInfo right)
    {
        bool leftIsDoor = left?.Building != null && left.Building.IsInteriorDoor;
        bool rightIsDoor = right?.Building != null && right.Building.IsInteriorDoor;
        if (leftIsDoor != rightIsDoor)
        {
            return leftIsDoor;
        }

        return true;
    }

    private static bool IsSingleCellRoomBoundary(BuildingSO building)
    {
        return building != null
            && building.width == 1
            && building.height == 1
            && (building.IsStructuralWall || building.IsInteriorDoor);
    }

    private void EnsureHallwayUnderBuildingFootprint(BuildingSO buildingData, Vector2Int position)
    {
        if (!RequiresHallwayUnderFootprint(buildingData) || hallwayBuilding == null)
        {
            return;
        }

        foreach (Vector2Int gridPos in buildingData.GetGridPosList(position))
        {
            GridCell cell = grid.GetGridCell(gridPos);
            if (cell == null || cell.HasBuildingInLayer(GridLayer.Hallway)) continue;

            PlaceBuildingWithoutValidation(hallwayBuilding, gridPos, out _);
        }
    }

    private bool IsDuplicateInitialHallway(InitialBuildInfo item)
    {
        if (item?.Building == null || item.Building.Placement.Layer != GridLayer.Hallway)
        {
            return false;
        }

        foreach (Vector2Int gridPos in item.Building.GetGridPosList(item.Position))
        {
            GridCell cell = grid?.GetGridCell(gridPos);
            if (cell == null || !cell.HasBuildingInLayer(GridLayer.Hallway)) return false;
        }

        return true;
    }

    private static bool RequiresHallwayUnderFootprint(BuildingSO buildingData)
    {
        if (buildingData == null)
        {
            return false;
        }

        GridBuildingPlacement placement = buildingData.Placement;
        return placement.Layer != GridLayer.Hallway
            && !placement.IsStructuralWall;
    }

    private bool PlaceBuildingWithoutValidation(BuildingSO buildingData, Vector2Int position, out string errorMessage)
    {
        if (!CanRegisterBuilding(buildingData, position))
        {
            errorMessage = $"{buildingData?.objectName ?? "Building"} cannot occupy the requested grid layer.";
            return false;
        }

        BuildableObject buildableObject = buildingFactory.Create(grid, buildingData, position);
        if (buildableObject == null)
        {
            errorMessage = buildingData != null
                ? $"{buildingData.objectName} 생성에 실패했습니다"
                : "건물 생성에 실패했습니다";
            return false;
        }

        buildableObject.SetGrid(grid);
        buildableObject.Initialization(buildingData, position);
        bool registered = grid.RegisterOccupant(
            buildableObject,
            buildingData.Placement.Layer,
            buildingData.GetGridPosList(position),
            buildingData.Placement.IsMovement);
        if (!registered)
        {
            buildingFactory.DeleteVisual(buildingData, position);
            buildableObject.DestroySelf();
            errorMessage = $"{buildingData.objectName} 그리드 등록에 실패했습니다";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private bool CreateConstructionSite(
        BuildingSO buildingData,
        Vector2Int position,
        out ConstructionSite site,
        out string errorMessage)
    {
        site = null;
        if (grid == null || buildingData == null)
        {
            errorMessage = "그리드 또는 건물 데이터가 없습니다.";
            return false;
        }

        foreach (Vector2Int gridPos in buildingData.GetGridPosList(position))
        {
            GridCell cell = grid.GetGridCell(gridPos);
            if (cell == null || !cell.CanOccupy(GridLayer.Construction))
            {
                errorMessage = "이미 공사가 진행 중인 칸입니다.";
                return false;
            }
        }

        GameObject siteObject = new GameObject($"ConstructionSite_{buildingData.objectName}_{position.x}_{position.y}");
        DungeonRuntimeHierarchy.Parent(siteObject, DungeonRuntimeHierarchy.Construction);
        site = siteObject.AddComponent<ConstructionSite>();
        onConstructionSiteCreated?.Invoke(site);
        site.transform.position = grid.GetWorldPos(position);
        site.SetGrid(grid);
        site.Initialization(buildingData, position);
        bool registered = grid.RegisterOccupant(
            site,
            GridLayer.Construction,
            buildingData.GetGridPosList(position),
            false);
        if (!registered)
        {
            UnityEngine.Object.Destroy(siteObject);
            site = null;
            errorMessage = "공사 현장을 그리드에 등록하지 못했습니다.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private void RemoveConstructionSite(ConstructionSite site)
    {
        if (site == null || grid == null)
        {
            return;
        }

        grid.RemoveOccupant(
            site,
            GridLayer.Construction,
            site.buildPoses,
            false);
        if (site != null)
        {
            UnityEngine.Object.Destroy(site.gameObject);
        }
    }

    private bool CanRegisterBuilding(BuildingSO buildingData, Vector2Int position)
    {
        if (grid == null || buildingData == null)
        {
            return false;
        }

        foreach (Vector2Int gridPos in buildingData.GetGridPosList(position))
        {
            GridCell cell = grid.GetGridCell(gridPos);
            if (cell == null
                || !cell.CanBuildInArea(buildingData)
                || !cell.CanOccupy(buildingData.Placement.Layer))
            {
                return false;
            }
        }

        return true;
    }

    private void RestoreReplacedWall(BuildableObject wall, ref string errorMessage)
    {
        if (wall == null)
        {
            return;
        }

        BuildingSO wallData = wall.BuildingData;
        bool restored = wallData != null && grid.RegisterOccupant(
            wall,
            wallData.Placement.Layer,
            wall.buildPoses,
            wallData.Placement.IsMovement);
        if (!restored)
        {
            errorMessage = "문 설치에 실패했고 기존 내벽을 복구하지 못했습니다.";
            Debug.LogError(errorMessage);
        }
    }
}

public interface IGridBuildingVisual
{
    void DrawBuilding(BuildingSO buildingData, Vector2Int position);
    void DeleteBuilding(BuildingSO buildingData, Vector2Int position);
}

public interface IGridBuildingFactory
{
    BuildableObject Create(Grid grid, BuildingSO buildingData, Vector2Int selectPos);
    void DeleteVisual(BuildingSO buildingData, Vector2Int selectPos);
}

public class GridBuildingFactory : IGridBuildingFactory
{
    private readonly Action<BuildableObject> onBuildingCreated;
    private readonly IGridBuildingVisual buildingVisual;
    private readonly IGridBuildingObjectFactory objectFactory;

    public GridBuildingFactory(IGridBuildingObjectFactory objectFactory)
        : this(null, null, objectFactory)
    {
    }

    public GridBuildingFactory(Action<BuildableObject> onBuildingCreated = null)
        : this(null, onBuildingCreated, new GridBuildingObjectFactory())
    {
    }

    public GridBuildingFactory(IGridBuildingVisual buildingVisual, Action<BuildableObject> onBuildingCreated = null)
        : this(buildingVisual, onBuildingCreated, new GridBuildingObjectFactory())
    {
    }

    public GridBuildingFactory(
        IGridBuildingVisual buildingVisual,
        Action<BuildableObject> onBuildingCreated,
        IGridBuildingObjectFactory objectFactory)
    {
        this.buildingVisual = buildingVisual;
        this.onBuildingCreated = onBuildingCreated;
        this.objectFactory = objectFactory ?? throw new ArgumentNullException(nameof(objectFactory));
    }

    public BuildableObject Create(Grid grid, BuildingSO buildingData, Vector2Int selectPos)
    {
        BuildableObject buildableObject = objectFactory.Create(grid, buildingData, selectPos);
        if (buildableObject == null) return null;

        buildingVisual?.DrawBuilding(buildingData, selectPos);
        ValidateBuildingVisual(buildingData);
        onBuildingCreated?.Invoke(buildableObject);
        return buildableObject;
    }

    public void DeleteVisual(BuildingSO buildingData, Vector2Int selectPos)
    {
        if (buildingData == null) return;

        buildingVisual?.DeleteBuilding(buildingData, selectPos);
    }

    private static void ValidateBuildingVisual(BuildingSO buildingData)
    {
        if (buildingData == null || buildingData.IsWall || HasTileVisual(buildingData) || buildingData.sprite != null)
        {
            return;
        }

        Debug.LogWarning($"{buildingData.objectName} has no tile or sprite visual data.");
    }

    private static bool HasTileVisual(BuildingSO buildingData)
    {
        return buildingData.tiles != null && buildingData.tiles.Count > 0;
    }

}

public class BuildingPlacementValidator
{
    private readonly GridPlacementValidator gridPlacementValidator;
    private readonly Func<BuildingConditionContext> conditionContextFactory;
    public IDungeonDebugRuleQuery DebugRules => CreateConditionContext().DebugRules;

    public BuildingPlacementValidator()
        : this(new GridPlacementValidator(), null)
    {
    }

    public BuildingPlacementValidator(GridPlacementValidator gridPlacementValidator)
        : this(gridPlacementValidator, null)
    {
    }

    public BuildingPlacementValidator(
        GridPlacementValidator gridPlacementValidator,
        Func<BuildingConditionContext> conditionContextFactory)
    {
        this.gridPlacementValidator = gridPlacementValidator ?? new GridPlacementValidator();
        this.conditionContextFactory = conditionContextFactory;
    }

    public bool CanBuild(Grid grid, BuildingSO buildingData, Vector2Int buildPos, out string errorMessage)
    {
        if (grid == null)
        {
            errorMessage = "그리드가 초기화되지 않았습니다";
            return false;
        }

        if (buildingData == null)
        {
            errorMessage = "설치할 건물이 선택되지 않았습니다";
            return false;
        }

        BuildingConditionContext context = CreateConditionContext();
        if (!FacilityProgression.IsUnlocked(
                buildingData,
                context.GameSessionState,
                context.BuildingUnlockState,
                context.DebugRules))
        {
            int phase = buildingData.GetUnlockPhase();
            errorMessage = $"{phase}단계 시설입니다. 운영일을 진행하거나 관련 설계도를 연구해야 합니다.";
            return false;
        }

        List<Vector2Int> totalBuildPos = buildingData.GetGridPosList(buildPos);
        if (!gridPlacementValidator.AreInsideHorizontalBounds(grid, totalBuildPos, 1))
        {
            errorMessage = "설치할 수 없는 위치입니다";
            return false;
        }

        if (buildingData.IsInteriorDoor
            && !GridDoorPlacementRules.TryGetTargetWall(grid, totalBuildPos, out _))
        {
            errorMessage = "문은 설치된 내벽 한 칸에만 설치할 수 있습니다.";
            return false;
        }

        if (!gridPlacementValidator.CanBuildInArea(grid, buildingData, totalBuildPos))
        {
            errorMessage = "이 구역에는 설치할 수 없습니다.";
            return false;
        }

        if (!buildingData.IsInteriorDoor
            && !gridPlacementValidator.CanOccupy(grid, buildingData.Placement.Layer, totalBuildPos))
        {
            errorMessage = "이미 설치 된 건물이 존재합니다";
            return false;
        }

        if (!gridPlacementValidator.HasSupportBelow(grid, totalBuildPos))
        {
            errorMessage = "바닥이 없습니다.";
            return false;
        }

        foreach (IBuildingCondition condition in buildingData.BuildConditions)
        {
            if (ShouldApplyBuildCondition(buildingData, condition)
                && !condition.IsSatisfy(
                    new BuildingConnectivityQueryAdapter(grid),
                    totalBuildPos,
                    context,
                    out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    public bool CanDestroy(Grid grid, BuildingSO buildingData, BuildableObject building, out string errorMessage)
    {
        if (grid == null)
        {
            errorMessage = "그리드가 초기화되지 않았습니다";
            return false;
        }

        if (buildingData == null || building == null)
        {
            errorMessage = "삭제할 건물이 없습니다";
            return false;
        }

        List<Vector2Int> buildedPos = buildingData.GetGridPosList(building.centerPos);
        if (!gridPlacementValidator.CanRemoveOccupantWithoutUnsupportedAbove(grid, buildedPos, building))
        {
            errorMessage = "윗층에 건물이 존재합니다";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public void ApplyBuildSuccess(BuildingSO buildingData)
    {
        if (buildingData == null) return;

        BuildingConditionContext context = CreateConditionContext();
        foreach (IBuildingCondition condition in buildingData.BuildConditions)
        {
            if (ShouldApplyBuildCondition(buildingData, condition))
            {
                condition.OnBuild(context);
            }
        }
    }

    public void ApplyDestroySuccess(BuildingSO buildingData)
    {
        // Demolition recovery is represented by physical materials.
    }

    private BuildingConditionContext CreateConditionContext()
    {
        return conditionContextFactory != null
            ? conditionContextFactory()
            : BuildingConditionContext.Empty;
    }

    private static bool ShouldApplyBuildCondition(BuildingSO buildingData, IBuildingCondition condition)
    {
        return condition != null
            && condition is not ConditionNeedMoney;
    }
}

public static class GridBuildingExtensions
{
    public static bool CanBuild(this GridCell cell, GridLayer layer = GridLayer.Building)
    {
        return cell != null && cell.CanOccupy(layer);
    }

    public static bool HasBuildingInLayer(this GridCell cell, GridLayer layer = GridLayer.Building)
    {
        return cell != null && cell.HasOccupantInLayer(layer);
    }

    public static bool HasBuilding(this GridCell cell)
    {
        return cell != null && cell.HasOccupant();
    }

    public static BuildableObject GetBuildingInlayer(this GridCell cell, GridLayer layer = GridLayer.Building)
    {
        return cell?.GetOccupant(layer) as BuildableObject;
    }

    public static BuildableObject GetBuilding(this GridCell cell)
    {
        if (cell == null)
        {
            return null;
        }

        return cell.GetAllOccupants()
            .OfType<BuildableObject>()
            .OrderByDescending(GetBuildingSelectionOrder)
            .FirstOrDefault();
    }

    private static int GetBuildingSelectionOrder(BuildableObject building)
    {
        if (building == null || building.BuildingData == null)
        {
            return 0;
        }

        if (building is ConstructionSite)
        {
            return 65;
        }

        return building.BuildingData.Placement.Layer switch
        {
            GridLayer.Building => 60,
            GridLayer.WallFixture => 50,
            GridLayer.CeilingFixture => 40,
            GridLayer.FloorOverlay => 30,
            GridLayer.Hallway => 10,
            _ => 0
        };
    }

    public static List<BuildableObject> GetAllBuilding(this GridCell cell)
    {
        if (cell == null) return new List<BuildableObject>();

        return cell.GetAllOccupants()
                   .OfType<BuildableObject>()
                   .ToList();
    }

    public static List<BuildableObject> GetAllVisitableBuilding(this GridPathSearchResult searchResult)
    {
        if (searchResult == null) return new List<BuildableObject>();

        return searchResult.GetAllVisitableOccupants()
                           .OfType<BuildableObject>()
                           .ToList();
    }

    public static List<BuildableObject> GetAllReachableBuilding(this GridPathSearchResult searchResult)
    {
        if (searchResult == null) return new List<BuildableObject>();

        return searchResult.GetAllReachableOccupants()
                           .OfType<BuildableObject>()
                           .ToList();
    }

    public static List<BuildableObject> GetAllVisitableBuilding(this Grid grid, Vector2Int start)
    {
        if (grid == null) return new List<BuildableObject>();

        return grid.GetAllVisitableOccupants(start)
                   .OfType<BuildableObject>()
                   .ToList();
    }

    public static List<BuildableObject> GetAllReachableBuilding(this Grid grid, Vector2Int start)
    {
        if (grid == null) return new List<BuildableObject>();

        return grid.GetAllReachableOccupants(start)
                   .OfType<BuildableObject>()
                   .ToList();
    }

    public static bool IsConneted(this Grid grid, Vector2Int start, int id)
    {
        return grid.IsConnected(start, id);
    }

    public static bool IsConnected(this Grid grid, Vector2Int start, int id)
    {
        return grid != null && grid.IsConnected(start, id);
    }

    public static List<BuildableObject> FindAllBuilding(this Grid grid, int id)
    {
        if (grid == null) return new List<BuildableObject>();

        return grid.FindAllOccupants((occupant) => occupant.GridId == id)
                   .OfType<BuildableObject>()
                   .ToList();
    }

    public static int CountBuilding(this Grid grid, BuildingSO buildingSO)
    {
        if (grid == null || buildingSO == null) return 0;

        return grid.FindAllOccupants((occupant) => occupant.GridId == buildingSO.id).Count;
    }

}
