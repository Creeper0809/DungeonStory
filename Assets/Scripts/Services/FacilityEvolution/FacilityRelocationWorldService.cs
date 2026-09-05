using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

public interface IFacilityRelocationWorldService
{
    bool CanRelocate(
        BuildableObject source,
        Vector2Int destination,
        out string failureReason);
    bool TryPackAtDestination(
        BuildableObject source,
        Vector2Int destination,
        out string failureReason);
    bool TryCompleteRelocation(
        BuildableObject packedSource,
        out BuildableObject relocated,
        out string failureReason);
    void RestorePackedPresentation(BuildableObject packedSource);
}

public sealed class FacilityRelocationWorldService :
    IFacilityRelocationWorldService
{
    private const string MarkerObjectName = "FacilityRelocationMarker";
    private readonly IGridTextureProvider gridTextureProvider;
    private readonly IGridBuildingObjectFactory objectFactory;
    private readonly IObjectResolver objectResolver;
    private readonly GridPlacementValidator placementValidator =
        new GridPlacementValidator();
    private readonly IProductionFacilityHandleQuery productionFacilityHandles =
        new ProductionFacilityHandleQueryAdapter();
    private Sprite markerSprite;

    public FacilityRelocationWorldService(
        IGridTextureProvider gridTextureProvider,
        IGridBuildingObjectFactory objectFactory,
        IObjectResolver objectResolver)
    {
        this.gridTextureProvider = gridTextureProvider
            ?? throw new ArgumentNullException(nameof(gridTextureProvider));
        this.objectFactory = objectFactory
            ?? throw new ArgumentNullException(nameof(objectFactory));
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
    }

    public bool CanRelocate(
        BuildableObject source,
        Vector2Int destination,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (source == null
            || source.isDestroy
            || source.Grid == null
            || source.BuildingData == null)
        {
            failureReason = "이전할 시설이 없습니다.";
            return false;
        }

        if (source is IWarehouseFacility warehouse
            && warehouse.HasWarehouseInventory)
        {
            IWarehouseLifecycleOccupancyQuery lifecycle =
                ResolveWarehouseLifecycleOccupancy();
            if (!lifecycle.TryRequireEmpty(
                    warehouse,
                    out _,
                    out string lifecycleFailure))
            {
                failureReason = "재고·예약·운반 중 화물이 남은 창고는 이전할 수 없습니다. "
                    + lifecycleFailure;
                return false;
            }
        }

        if (source is IRetailFacility retail
            && (retail.CurrentStock > 0
                || retail.HasWaitingCheckout
                || retail.HasServingWorker
                || (source as IRetailRestockOperationOwner)?
                    .ActiveRestockOperationCount > 0))
        {
            failureReason = "재고·고객·직원·보충 중 화물이 남은 상점은 이전할 수 없습니다.";
            return false;
        }

        BuildingSO building = source.BuildingData;
        GridBuildingPlacement placement = building.Placement;
        if (source is not IWorkableFacility
            || placement.IsStructuralWall
            || placement.IsMovement
            || building.IsInteriorDoor)
        {
            failureReason = "벽, 문, 이동 시설은 일반 시설 이전을 사용할 수 없습니다.";
            return false;
        }

        if (destination == source.centerPos)
        {
            failureReason = "현재 위치와 다른 칸을 선택해야 합니다.";
            return false;
        }

        Grid grid = source.Grid;
        List<Vector2Int> positions = building.GetGridPosList(destination);
        if (!placementValidator.AreInsideHorizontalBounds(grid, positions, 1)
            || !placementValidator.CanBuildInArea(grid, building, positions)
            || !placementValidator.HasSupportBelow(grid, positions))
        {
            failureReason = "선택한 위치는 시설을 재설치할 수 없습니다.";
            return false;
        }

        foreach (Vector2Int position in positions)
        {
            GridCell cell = grid.GetGridCell(position);
            if (cell == null
                || !cell.CanOccupy(placement.Layer)
                || !cell.CanOccupy(GridLayer.Construction))
            {
                failureReason = "선택한 위치에 다른 시설이나 공사 현장이 있습니다.";
                return false;
            }
        }

        return true;
    }

    private IWarehouseLifecycleOccupancyQuery ResolveWarehouseLifecycleOccupancy()
    {
        if (objectResolver.TryResolve(
                typeof(IWarehouseLifecycleOccupancyQuery),
                out object resolved)
            && resolved is IWarehouseLifecycleOccupancyQuery occupancy)
        {
            return occupancy;
        }

        throw new InvalidOperationException(
            $"{nameof(FacilityRelocationWorldService)} requires "
            + $"{nameof(IWarehouseLifecycleOccupancyQuery)}.");
    }

    private IProductionFacilityMutationFence ResolveProductionFacilityMutationFence()
    {
        if (objectResolver.TryResolve(
                typeof(IProductionFacilityMutationFence),
                out object resolved)
            && resolved is IProductionFacilityMutationFence fence)
        {
            return fence;
        }
        throw new InvalidOperationException(
            $"{nameof(FacilityRelocationWorldService)} requires "
            + $"{nameof(IProductionFacilityMutationFence)}.");
    }

    private IProductionFacilityRetargetTransaction ResolveProductionFacilityRetargetTransaction()
    {
        if (objectResolver.TryResolve(
                typeof(IProductionFacilityRetargetTransaction),
                out object resolved)
            && resolved is IProductionFacilityRetargetTransaction transaction)
        {
            return transaction;
        }
        throw new InvalidOperationException(
            $"{nameof(FacilityRelocationWorldService)} requires "
            + $"{nameof(IProductionFacilityRetargetTransaction)}.");
    }

    public bool TryPackAtDestination(
        BuildableObject source,
        Vector2Int destination,
        out string failureReason)
    {
        if (!CanRelocate(source, destination, out failureReason))
        {
            return false;
        }

        Grid grid = source.Grid;
        BuildingSO building = source.BuildingData;
        GridBuildingPlacement placement = building.Placement;
        GridBuildingFactory factory = CreateFactory();
        Vector2Int sourcePosition = source.centerPos;
        IReadOnlyList<Vector2Int> sourcePositions =
            source.buildPoses.ToArray();
        if (!grid.RemoveOccupant(
                source,
                placement.Layer,
                sourcePositions,
                placement.IsMovement))
        {
            failureReason = "기존 시설 점유를 해제하지 못했습니다.";
            return false;
        }

        factory.DeleteVisual(building, sourcePosition);
        SetPermanentVisualsEnabled(source, false);
        source.SetRuntimeGridPosition(destination);
        if (!grid.RegisterOccupant(
                source,
                GridLayer.Construction,
                source.buildPoses,
                false))
        {
            source.SetRuntimeGridPosition(sourcePosition);
            grid.RegisterOccupant(
                source,
                placement.Layer,
                sourcePositions,
                placement.IsMovement);
            SetPermanentVisualsEnabled(source, true);
            failureReason = "재설치 현장을 예약하지 못했습니다.";
            return false;
        }

        EnsureMarker(source);
        failureReason = string.Empty;
        return true;
    }

    public bool TryCompleteRelocation(
        BuildableObject packedSource,
        out BuildableObject relocated,
        out string failureReason)
    {
        relocated = null;
        failureReason = string.Empty;
        if (packedSource == null
            || packedSource.Grid == null
            || packedSource.BuildingData == null)
        {
            failureReason = "포장된 시설을 찾을 수 없습니다.";
            return false;
        }

        Grid grid = packedSource.Grid;
        BuildingSO building = packedSource.BuildingData;
        Vector2Int destination = packedSource.centerPos;
        BuildingInstanceId survivorId = packedSource.RequirePersistentInstanceId();
        List<BuildingStateModuleSaveData> stateModules =
            BuildingStateModulePersistence.Capture(packedSource);
        GridBuildingFactory factory = CreateFactory();
        IProductionFacilityRetargetTransaction retarget =
            ResolveProductionFacilityRetargetTransaction();
        ProductionFacilityRetargetRequest retargetRequest = new(
            productionFacilityHandles.CaptureFacility(packedSource),
            ProductionFacilityMutationKind.Relocation);
        if (!retarget.TryBegin(
                new[] { retargetRequest },
                "relocation:" + survivorId.Value,
                out ProductionFacilityRetargetTransactionState retargetState,
                out string retargetBeginFailure))
        {
            failureReason = "생산 권위 이전 사전 검증 실패: "
                + retargetBeginFailure;
            return false;
        }
        BuildableObject created = null;
        try
        {
            created = factory.CreateDetached(grid, building, destination);
            if (created == null)
            {
                RollbackRetargetOrThrow(retarget, retargetState, "candidate-creation");
                failureReason = "이전한 시설 후보를 생성하지 못했습니다.";
                return false;
            }
            created.RestorePersistentIdentity(survivorId);
            created.SetGrid(grid);
            created.Initialization(building, destination);
        }
        catch (Exception exception)
        {
            DiscardRelocationCandidate(factory, created, building, destination);
            RollbackRetargetOrThrow(retarget, retargetState, "candidate-initialization");
            failureReason = "이전한 시설 후보를 초기화하지 못했습니다. "
                + exception.Message;
            return false;
        }

        if (!grid.RemoveOccupant(
                packedSource,
                GridLayer.Construction,
                packedSource.buildPoses,
                false))
        {
            DiscardRelocationCandidate(factory, created, building, destination);
            RollbackRetargetOrThrow(retarget, retargetState, "reservation-removal");
            failureReason = "재설치 현장 점유를 해제하지 못했습니다.";
            return false;
        }

        if (!grid.RegisterOccupant(
                created,
                building.Placement.Layer,
                created.buildPoses,
                building.Placement.IsMovement))
        {
            DiscardRelocationCandidate(factory, created, building, destination);
            RestorePackedReservation(grid, packedSource);
            RollbackRetargetOrThrow(retarget, retargetState, "candidate-registration");
            failureReason = "이전한 시설을 그리드에 등록하지 못했습니다.";
            return false;
        }

        BuildingStateModuleRestoreResult restore =
            BuildingStateModulePersistence.Restore(created, stateModules);
        if (!restore.Success)
        {
            grid.RemoveOccupant(
                created,
                building.Placement.Layer,
                created.buildPoses,
                building.Placement.IsMovement);
            DiscardRelocationCandidate(factory, created, building, destination);
            RestorePackedReservation(grid, packedSource);
            RollbackRetargetOrThrow(retarget, retargetState, "state-restore");
            failureReason = string.Join(" / ", restore.errors);
            return false;
        }

        IBuildingWorldRegistryPort worldRegistry = packedSource.WorldRegistry;
        string registryFailure = worldRegistry == null
            ? "building-world-registry-unavailable"
            : string.Empty;
        if (worldRegistry == null
            || !worldRegistry.TryReplaceBuilding(
                packedSource,
                created,
                out registryFailure))
        {
            grid.RemoveOccupant(
                created,
                building.Placement.Layer,
                created.buildPoses,
                building.Placement.IsMovement);
            DiscardRelocationCandidate(factory, created, building, destination);
            RestorePackedReservation(grid, packedSource);
            RollbackRetargetOrThrow(retarget, retargetState, "world-registry-handoff");
            failureReason = "이전 시설의 월드 권위를 교체하지 못했습니다. "
                + registryFailure;
            return false;
        }

        bool authorityCommitted;
        string retargetCommitFailure;
        try
        {
            ProductionFacilityHandle targetFacility =
                productionFacilityHandles.CaptureFacility(created);
            authorityCommitted = retarget.TryCommit(
                retargetState,
                new[]
                {
                    new ProductionFacilityRetargetBinding(
                        survivorId,
                        targetFacility)
                },
                out retargetCommitFailure);
        }
        catch (Exception exception)
        {
            authorityCommitted = false;
            retargetCommitFailure = "target-capture-or-commit-exception:"
                + exception.GetType().Name + ":" + exception.Message;
        }
        if (!authorityCommitted)
        {
            RollbackRetargetOrThrow(retarget, retargetState, "authority-commit");
            if (!worldRegistry.TryRollbackBuildingReplacement(
                    created,
                    packedSource,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Relocation authority commit failed and world rollback failed: "
                    + rollbackFailure);
            }
            grid.RemoveOccupant(
                created,
                building.Placement.Layer,
                created.buildPoses,
                building.Placement.IsMovement);
            DiscardRelocationCandidate(factory, created, building, destination);
            RestorePackedReservation(grid, packedSource);
            failureReason = "생산 권위 이전 반영 실패로 포장 상태를 복구했습니다. "
                + retargetCommitFailure;
            return false;
        }

        try
        {
            factory.PublishDetached(created, building, destination);
        }
        catch (Exception exception)
        {
            RollbackRetargetOrThrow(retarget, retargetState, "publication");
            if (!worldRegistry.TryRollbackBuildingReplacement(
                    created,
                    packedSource,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Relocation publication failed and world authority rollback also failed: "
                    + rollbackFailure,
                    exception);
            }
            grid.RemoveOccupant(
                created,
                building.Placement.Layer,
                created.buildPoses,
                building.Placement.IsMovement);
            DiscardRelocationCandidate(factory, created, building, destination);
            RestorePackedReservation(grid, packedSource);
            failureReason = "이전 시설 게시에 실패해 원본을 복구했습니다. "
                + exception.Message;
            return false;
        }

        if (!retarget.TryComplete(retargetState, out string retargetCompleteFailure))
        {
            RollbackRetargetOrThrow(retarget, retargetState, "completion");
            if (!worldRegistry.TryRollbackBuildingReplacement(
                    created,
                    packedSource,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Relocation authority completion failed and world rollback failed: "
                    + rollbackFailure);
            }
            grid.RemoveOccupant(
                created,
                building.Placement.Layer,
                created.buildPoses,
                building.Placement.IsMovement);
            DiscardRelocationCandidate(factory, created, building, destination);
            RestorePackedReservation(grid, packedSource);
            failureReason = "생산 권위 이전 완료 검증 실패로 포장 상태를 복구했습니다. "
                + retargetCompleteFailure;
            return false;
        }

        packedSource.SetGrid(null);
        packedSource.RetireForWorldReplacement();
        relocated = created;
        return true;
    }

    private static void RollbackRetargetOrThrow(
        IProductionFacilityRetargetTransaction retarget,
        ProductionFacilityRetargetTransactionState state,
        string phase)
    {
        if (state == null
            || state.Phase is ProductionFacilityRetargetTransactionPhase
                .RolledBack or ProductionFacilityRetargetTransactionPhase.Completed)
        {
            return;
        }
        if (!retarget.TryRollback(state, out string failureReason))
        {
            throw new InvalidOperationException(
                "Facility relocation retarget rollback failed during " + phase
                + ":" + failureReason);
        }
    }

    public void RestorePackedPresentation(BuildableObject packedSource)
    {
        if (packedSource == null)
        {
            return;
        }

        SetPermanentVisualsEnabled(packedSource, false);
        EnsureMarker(packedSource);
    }

    private GridBuildingFactory CreateFactory()
    {
        return new GridBuildingFactory(
            gridTextureProvider.Texture,
            building =>
            {
                if (building != null)
                {
                    objectResolver.Inject(building);
                }
            },
            objectFactory);
    }

    private void RestorePackedReservation(
        Grid grid,
        BuildableObject packedSource)
    {
        grid.RegisterOccupant(
            packedSource,
            GridLayer.Construction,
            packedSource.buildPoses,
            false);
        EnsureMarker(packedSource);
    }

    private static void DiscardRelocationCandidate(
        GridBuildingFactory factory,
        BuildableObject candidate,
        BuildingSO building,
        Vector2Int destination)
    {
        if (candidate == null)
        {
            return;
        }

        factory.DeleteVisual(building, destination);
        candidate.SetGrid(null);
        if (candidate.IsDetachedRestoreCandidate)
            factory.DiscardDetached(candidate);
        else
            candidate.RetireForWorldReplacement();
    }

    private void EnsureMarker(BuildableObject source)
    {
        Transform existing = source.transform.Find(MarkerObjectName);
        GameObject marker = existing != null
            ? existing.gameObject
            : new GameObject(MarkerObjectName);
        marker.transform.SetParent(source.transform, false);
        marker.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        SpriteRenderer renderer =
            marker.GetComponent<SpriteRenderer>()
            ?? marker.AddComponent<SpriteRenderer>();
        renderer.sprite = markerSprite != null
            ? markerSprite
            : markerSprite = CreateMarkerSprite();
        renderer.color = new Color(0.96f, 0.79f, 0.28f, 0.9f);
        renderer.sortingLayerName = "DungeonMiddleObject";
        renderer.sortingOrder = 66;
    }

    private static void SetPermanentVisualsEnabled(
        BuildableObject source,
        bool enabled)
    {
        foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(
                     includeInactive: true))
        {
            if (renderer != null
                && renderer.gameObject.name != MarkerObjectName)
            {
                renderer.enabled = enabled;
            }
        }
    }

    private static Sprite CreateMarkerSprite()
    {
        Texture2D texture = new Texture2D(
            12,
            12,
            TextureFormat.RGBA32,
            false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color solid = Color.white;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                bool edge = x == 1
                    || x == texture.width - 2
                    || y == 1
                    || y == texture.height - 2;
                bool strap = x == 5 || x == 6;
                texture.SetPixel(x, y, edge || strap ? solid : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0f),
            12f);
    }
}
