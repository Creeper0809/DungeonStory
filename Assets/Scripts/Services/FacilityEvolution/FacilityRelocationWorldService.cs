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
        List<BuildingStateModuleSaveData> stateModules =
            BuildingStateModulePersistence.Capture(packedSource);
        if (!grid.RemoveOccupant(
                packedSource,
                GridLayer.Construction,
                packedSource.buildPoses,
                false))
        {
            failureReason = "재설치 현장 점유를 해제하지 못했습니다.";
            return false;
        }

        GridBuildingFactory factory = CreateFactory();
        BuildableObject created = factory.Create(grid, building, destination);
        if (created == null)
        {
            RestorePackedReservation(grid, packedSource);
            failureReason = "이전한 시설을 생성하지 못했습니다.";
            return false;
        }

        created.SetGrid(grid);
        created.Initialization(building, destination);
        if (!grid.RegisterOccupant(
                created,
                building.Placement.Layer,
                created.buildPoses,
                building.Placement.IsMovement))
        {
            factory.DeleteVisual(building, destination);
            created.SetGrid(null);
            created.isDestroy = true;
            UnityEngine.Object.Destroy(created.gameObject);
            RestorePackedReservation(grid, packedSource);
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
            factory.DeleteVisual(building, destination);
            created.SetGrid(null);
            created.isDestroy = true;
            UnityEngine.Object.Destroy(created.gameObject);
            RestorePackedReservation(grid, packedSource);
            failureReason = string.Join(" / ", restore.errors);
            return false;
        }

        packedSource.SetGrid(null);
        packedSource.isDestroy = true;
        UnityEngine.Object.Destroy(packedSource.gameObject);
        relocated = created;
        return true;
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
