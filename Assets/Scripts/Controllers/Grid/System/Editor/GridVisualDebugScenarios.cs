using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class GridVisualDebugScenarios
{
    private static readonly IBlueprintResearchWorkService BlueprintResearchWorkService =
        new NoopBlueprintResearchWorkService();
    private static readonly IWorldInfoClickSelector WorldInfoClickSelector =
        new NoopWorldInfoClickSelector();
    private static readonly IFacilityCandidateCache FacilityCandidateCacheService =
        new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry);
    private static readonly IRoomFacilityPolicy RoomFacilityPolicyService =
        new RoomFacilityPolicyService(RoomRegistry.EditorCache);

    [MenuItem("DungeonStory/Debug/Grid Foundation/Run Visual Alignment Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("Grid visual alignment scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("sprite tile visual footprint alignment", VerifySpriteTileTransformMatchesGridFootprint, errors);
        RunScenario("structural wall keeps full-height render", VerifyStructuralWallKeepsFullHeightRender, errors);
        RunScenario("runtime markers do not shift automatic outer walls", VerifyRuntimeMarkersDoNotShiftAutomaticOuterWalls, errors);
        RunScenario("interior wall ceiling overlay renders in front", VerifyInteriorWallCeilingOverlayRendersInFront, errors);
        RunScenario("dungeon door keeps original three-cell art", VerifyDungeonDoorKeepsOriginalArt, errors);
        RunScenario("interior door fits one wall cell", VerifyInteriorDoorFitsOneCell, errors);
        RunScenario("interior door stays grounded in preserved wall", VerifyInteriorDoorStaysGroundedInPreservedWall, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Grid visual alignment scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)
    {
        for (int i = 0; i < 10; i++)
        {
            if (scenario()) continue;

            errors.Add($"{name} failed on pass {i + 1}.");
            return;
        }
    }

    private static bool VerifySpriteTileTransformMatchesGridFootprint()
    {
        float expectedVisualHeight = GridBuildingTileTransformCalculator.GetVisualFootprintSize(
            new Vector2(1f, GridBuildingTileTransformCalculator.DefaultCellTileHeight)).y;

        return VerifySpriteTileTransform(
                "Assets/Resources/SO/Building/P1/P1_RestRoom.asset",
                expectedCenterOffsetX: 0.5f,
                expectedCenterOffsetY: expectedVisualHeight * 0.5f,
                expectedWidth: 3f,
                expectedHeight: expectedVisualHeight,
                tilemapLocalOffset: Vector2.zero)
            && VerifySpriteTileTransform(
                "Assets/Resources/SO/Building/P1/P1_RestRoom.asset",
                expectedCenterOffsetX: 0.5f,
                expectedCenterOffsetY: expectedVisualHeight * 0.5f,
                expectedWidth: 3f,
                expectedHeight: expectedVisualHeight,
                tilemapLocalOffset: new Vector2(0f, -0.5f))
            && VerifySpriteTileTransform(
                "Assets/Resources/SO/Building/P1/P1_ResearchLab.asset",
                expectedCenterOffsetX: 1f,
                expectedCenterOffsetY: expectedVisualHeight * 0.5f,
                expectedWidth: 4f,
                expectedHeight: expectedVisualHeight,
                tilemapLocalOffset: new Vector2(0.5f, -0.5f))
            && VerifySpriteTileTransform(
                "Assets/Resources/SO/Building/P1/P1_MeatRestaurant.asset",
                expectedCenterOffsetX: 1f,
                expectedCenterOffsetY: expectedVisualHeight * 0.5f,
                expectedWidth: 4f,
                expectedHeight: expectedVisualHeight,
                tilemapLocalOffset: new Vector2(0.5f, -0.5f));
    }

    private static bool VerifyStructuralWallKeepsFullHeightRender()
    {
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        if (wall == null || wall.sprite == null || !wall.IsStructuralWall)
        {
            return false;
        }

        Grid grid = new Grid(5, 1);
        GridBuildingPlacementService placement = new GridBuildingPlacementService(
            grid,
            null,
            null,
            new GridBuildingFactory(InjectBuildingDependencies),
            new BuildingPlacementValidator());
        Vector2Int wallPosition = new Vector2Int(2, 0);
        bool placed = placement.TryPlaceBuilding(wall, wallPosition, out _);
        BuildableObject placedWall = grid.GetGridCell(wallPosition)?.GetBuildingInlayer(GridLayer.Building);

        GameObject root = new GameObject("StructuralWallVisualTest", typeof(UnityEngine.Grid));
        GameObject tilemapObject = new GameObject("Wall", typeof(Tilemap), typeof(TilemapRenderer));
        tilemapObject.transform.SetParent(root.transform, false);
        Tilemap tilemap = tilemapObject.GetComponent<Tilemap>();
        Tile wallTile = ScriptableObject.CreateInstance<Tile>();
        wallTile.sprite = wall.sprite;
        Tile floorTile = ScriptableObject.CreateInstance<Tile>();
        floorTile.sprite = wall.sprite;

        GridTexture texture = root.AddComponent<GridTexture>();
        texture.wallTilemap = tilemap;
        texture.wall = wallTile;
        texture.floor = floorTile;
        texture.DrawWall(grid);

        bool rendered = placed
            && placedWall != null
            && tilemap.GetTile(new Vector3Int(-2, 0, 0)) == wallTile
            && tilemap.GetTile(new Vector3Int(-2, 1, 0)) == wallTile
            && tilemap.GetTile(new Vector3Int(-2, 2, 0)) == wallTile
            && !tilemap.HasTile(new Vector3Int(-1, 0, 0))
            && !tilemap.HasTile(new Vector3Int(-3, 0, 0));

        if (placedWall != null)
        {
            Object.DestroyImmediate(placedWall.gameObject);
        }

        Object.DestroyImmediate(root);
        Object.DestroyImmediate(wallTile);
        Object.DestroyImmediate(floorTile);
        return rendered;
    }

    private static bool VerifyRuntimeMarkersDoNotShiftAutomaticOuterWalls()
    {
        Grid grid = new Grid(5, 1);
        TestGridOccupant content = new TestGridOccupant(1);
        TestGridOccupant floorMarker = new TestGridOccupant(2);
        TestGridOccupant wallMarker = new TestGridOccupant(3);
        TestGridOccupant ceilingMarker = new TestGridOccupant(4);
        Vector2Int markerPosition = new Vector2Int(2, 0);
        Vector2Int contentPosition = new Vector2Int(3, 0);

        bool registered = grid.RegisterOccupant(
                content,
                GridLayer.Hallway,
                new[] { contentPosition },
                false)
            && grid.RegisterOccupant(
                floorMarker,
                GridLayer.FloorOverlay,
                new[] { markerPosition },
                false)
            && grid.RegisterOccupant(
                wallMarker,
                GridLayer.WallFixture,
                new[] { markerPosition },
                false)
            && grid.RegisterOccupant(
                ceilingMarker,
                GridLayer.CeilingFixture,
                new[] { markerPosition },
                false);

        HashSet<Vector2Int> walls = new GridWallTileCalculator().GetWallTilePositions(grid);
        return registered
            && walls.Contains(markerPosition)
            && !walls.Contains(markerPosition + Vector2Int.left);
    }

    private static bool VerifyInteriorWallCeilingOverlayRendersInFront()
    {
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        BuildingSO hallway = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Hallway.asset");
        if (wall == null || hallway == null || !wall.IsStructuralWall)
        {
            return false;
        }

        Grid grid = new Grid(5, 1);
        GridBuildingPlacementService placement = new GridBuildingPlacementService(
            grid,
            hallway,
            null,
            new GridBuildingFactory(InjectBuildingDependencies),
            new BuildingPlacementValidator());
        bool leftPlaced = placement.TryPlaceBuilding(hallway, new Vector2Int(1, 0), out _);
        bool wallPlaced = placement.TryPlaceBuilding(wall, new Vector2Int(2, 0), out _);
        bool rightPlaced = placement.TryPlaceBuilding(hallway, new Vector2Int(3, 0), out _);

        GameObject root = new GameObject("InteriorWallCeilingOverlayTest", typeof(UnityEngine.Grid));
        GameObject wallTilemapObject = new GameObject("Wall", typeof(Tilemap), typeof(TilemapRenderer));
        wallTilemapObject.transform.SetParent(root.transform, false);
        Tilemap wallTilemap = wallTilemapObject.GetComponent<Tilemap>();
        TilemapRenderer wallRenderer = wallTilemapObject.GetComponent<TilemapRenderer>();
        wallRenderer.sortingLayerName = "Wall";
        wallRenderer.sortingOrder = 100;
        Tile wallTile = ScriptableObject.CreateInstance<Tile>();
        wallTile.sprite = wall.sprite;
        Tile floorTile = ScriptableObject.CreateInstance<Tile>();
        floorTile.sprite = hallway.sprite;

        GridTexture texture = root.AddComponent<GridTexture>();
        texture.wallTilemap = wallTilemap;
        texture.wall = wallTile;
        texture.floor = floorTile;
        texture.DrawWall(grid);

        Tilemap overlayTilemap = null;
        foreach (Tilemap candidate in root.GetComponentsInChildren<Tilemap>())
        {
            if (candidate != null && candidate != wallTilemap && candidate.name == "CeilingOverlay")
            {
                overlayTilemap = candidate;
                break;
            }
        }

        TilemapRenderer overlayRenderer = overlayTilemap != null
            ? overlayTilemap.GetComponent<TilemapRenderer>()
            : null;
        int topY = GridBuildingTileTransformCalculator.DefaultCellTileHeight - 1;
        bool valid = leftPlaced
            && wallPlaced
            && rightPlaced
            && overlayTilemap != null
            && overlayRenderer != null
            && wallTilemap.GetTile(new Vector3Int(-2, topY, 0)) == wallTile
            && overlayTilemap.GetTile(new Vector3Int(-2, topY, 0)) == floorTile
            && overlayTilemap.GetTile(new Vector3Int(-1, topY, 0)) == floorTile
            && overlayTilemap.GetTile(new Vector3Int(-3, topY, 0)) == floorTile
            && overlayRenderer.sortingLayerName == wallRenderer.sortingLayerName
            && overlayRenderer.sortingOrder > wallRenderer.sortingOrder
            && overlayRenderer.sortingOrder > InteriorDoorVisualLayout.SortingOrder;

        Object.DestroyImmediate((grid.GetGridCell(new Vector2Int(1, 0))?.GetOccupant(GridLayer.Hallway) as Component)?.gameObject);
        Object.DestroyImmediate((grid.GetGridCell(new Vector2Int(2, 0))?.GetOccupant(GridLayer.Building) as Component)?.gameObject);
        Object.DestroyImmediate((grid.GetGridCell(new Vector2Int(3, 0))?.GetOccupant(GridLayer.Hallway) as Component)?.gameObject);
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(wallTile);
        Object.DestroyImmediate(floorTile);
        return valid;
    }

    private static bool VerifyDungeonDoorKeepsOriginalArt()
    {
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Door.asset");
        BuildingSO hallway = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Hallway.asset");
        Tile doorTile = null;
        bool hasDoorTile = door?.tiles != null
            && door.tiles.TryGetValue(GridTexture.TilemapLayer.BACK, out doorTile);
        if (door == null
            || hallway == null
            || !door.IsDoor
            || door.IsInteriorDoor
            || door.type != typeof(Door)
            || door.width != 3
            || door.height != 1
            || !hasDoorTile
            || doorTile == null
            || doorTile.sprite == null)
        {
            Debug.Log($"Dungeon door asset header invalid: door={door != null},hallway={hallway != null},"
                + $"isDoor={door?.IsDoor},interior={door?.IsInteriorDoor},type={door?.type?.Name},"
                + $"size={door?.width}x{door?.height},unlocked={door?.unlocked},"
                + $"tiles={door?.tiles?.Count ?? -1},tile={doorTile != null},sprite={doorTile?.sprite != null}");
            return false;
        }

        Bounds spriteBounds = doorTile.sprite.bounds;
        Vector3 scale = doorTile.transform.lossyScale;
        Vector2 renderedSize = new Vector2(
            Mathf.Abs(spriteBounds.size.x * scale.x),
            Mathf.Abs(spriteBounds.size.y * scale.y));
        Vector3 renderedCenter = doorTile.transform.MultiplyPoint3x4(spriteBounds.center);
        float renderedRectMinY = renderedCenter.y - renderedSize.y * 0.5f;
        float renderedRectMaxY = renderedCenter.y + renderedSize.y * 0.5f;
        float axisScaleRatio = Mathf.Abs(scale.x / scale.y);

        bool assetValid = door.GetGridPosList(new Vector2Int(2, 0)).Count == 3
            && door.sprite == doorTile.sprite
            && door.icon == doorTile.sprite
            && doorTile.sprite.name == "Door"
            && AssetDatabase.GetAssetPath(doorTile.sprite) == "Assets/Images/Using/Door.png"
            && doorTile.sprite.rect.width == 48f
            && doorTile.sprite.rect.height == 48f
            && doorTile.color.a >= 0.99f
            && Mathf.Abs(axisScaleRatio - 1f) <= 0.01f
            && Mathf.Abs(renderedSize.x - 3f) <= 0.01f
            && Mathf.Abs(renderedSize.y - 3f) <= 0.01f
            && Mathf.Abs(renderedRectMinY) <= 0.01f
            && Mathf.Abs(renderedRectMaxY - 3f) <= 0.01f;
        if (!assetValid)
        {
            Debug.Log($"Dungeon door source art invalid: poses={door.GetGridPosList(new Vector2Int(2, 0)).Count},"
                + $"spriteMatch={door.sprite == doorTile.sprite},iconMatch={door.icon == doorTile.sprite},"
                + $"sprite={doorTile.sprite.name},path={AssetDatabase.GetAssetPath(doorTile.sprite)},"
                + $"rect={doorTile.sprite.rect.size},alpha={doorTile.color.a:F2},"
                + $"scaleRatio={axisScaleRatio:F2},size={renderedSize},"
                + $"y={renderedRectMinY:F2}->{renderedRectMaxY:F2}");
            return false;
        }

        Grid grid = new Grid(7, 1);
        GridBuildingPlacementService placement = new GridBuildingPlacementService(
            grid,
            hallway,
            null,
            new GridBuildingFactory(InjectBuildingDependencies),
            new BuildingPlacementValidator());
        Vector2Int doorCenter = new Vector2Int(3, 0);
        placement.PlaceInitialBuildings(new[]
        {
            new InitialBuildInfo
            {
                Position = doorCenter,
                Building = door
            }
        });
        Door placedDoor = grid.GetGridCell(new Vector2Int(3, 0))
            ?.GetBuildingInlayer(GridLayer.Building) as Door;
        bool placed = placedDoor != null;
        SpriteRenderer visual = placedDoor != null ? placedDoor.VisualRenderer : null;

        GameObject wallRoot = new GameObject("DungeonDoorWallClearTest", typeof(UnityEngine.Grid));
        GameObject wallTilemapObject = new GameObject("Wall", typeof(Tilemap), typeof(TilemapRenderer));
        wallTilemapObject.transform.SetParent(wallRoot.transform, false);
        Tilemap wallTilemap = wallTilemapObject.GetComponent<Tilemap>();
        GameObject hallwayTilemapObject = new GameObject("Hallway", typeof(Tilemap), typeof(TilemapRenderer));
        hallwayTilemapObject.transform.SetParent(wallRoot.transform, false);
        Tilemap hallwayTilemap = hallwayTilemapObject.GetComponent<Tilemap>();
        Tile wallTile = ScriptableObject.CreateInstance<Tile>();
        Tile floorTile = ScriptableObject.CreateInstance<Tile>();
        Tile staleHallwayTile = ScriptableObject.CreateInstance<Tile>();
        for (int x = 2; x <= 4; x++)
        {
            for (int y = 0; y < GridBuildingTileTransformCalculator.DefaultCellTileHeight; y++)
            {
                wallTilemap.SetTile(new Vector3Int(-x, y, 0), wallTile);
            }

            hallwayTilemap.SetTile(new Vector3Int(-x, 0, 0), staleHallwayTile);
        }

        GridTexture texture = wallRoot.AddComponent<GridTexture>();
        texture.wallTilemap = wallTilemap;
        texture.wall = wallTile;
        texture.floor = floorTile;
        texture.buildingTilemapOdd = new Dictionary<GridTexture.TilemapLayer, Tilemap>
        {
            [GridTexture.TilemapLayer.HALLWAY] = hallwayTilemap
        };
        texture.DrawBuilding(door, new Vector2Int(3, 0));
        bool directHallwayFootprintCleared = true;
        for (int x = 2; x <= 4; x++)
        {
            if (hallwayTilemap.HasTile(new Vector3Int(-x, 0, 0)))
            {
                directHallwayFootprintCleared = false;
            }

            hallwayTilemap.SetTile(new Vector3Int(-x, 0, 0), staleHallwayTile);
        }

        texture.DrawWall(grid);

        bool wallFootprintCleared = true;
        bool hallwayFootprintCleared = true;
        bool ceilingTilesPreserved = true;
        for (int x = 2; x <= 4; x++)
        {
            for (int y = 0; y < GridBuildingTileTransformCalculator.DefaultCellTileHeight; y++)
            {
                if (wallTilemap.GetTile(new Vector3Int(-x, y, 0)) == wallTile)
                {
                    wallFootprintCleared = false;
                }
            }

            if (hallwayTilemap.HasTile(new Vector3Int(-x, 0, 0)))
            {
                hallwayFootprintCleared = false;
            }

            if (wallTilemap.GetTile(new Vector3Int(
                    -x,
                    GridBuildingTileTransformCalculator.DefaultCellTileHeight - 1,
                    0)) != floorTile)
            {
                ceilingTilesPreserved = false;
            }
        }

        int doorLayerValue = SortingLayer.GetLayerValueFromName(
            DungeonDoorVisualLayout.SortingLayerName);
        int traversalLayerValue = SortingLayer.GetLayerValueFromName(
            DungeonDoorVisualLayout.TraversalSortingLayerName);
        int ceilingLayerValue = SortingLayer.GetLayerValueFromName(
            DungeonDoorVisualLayout.CeilingSortingLayerName);
        int defaultCharacterLayerValue = SortingLayer.GetLayerValueFromName(
            DungeonDoorVisualLayout.DefaultCharacterSortingLayerName);
        bool traversalOrderingValid = traversalLayerValue < doorLayerValue
            && doorLayerValue == ceilingLayerValue
            && DungeonDoorVisualLayout.SortingOrder < DungeonDoorVisualLayout.CeilingSortingOrder
            && ceilingLayerValue < defaultCharacterLayerValue;

        bool runtimeValid = placed
            && placedDoor != null
            && placedDoor.GetType() == typeof(Door)
            && visual != null
            && visual.transform.name == DungeonDoorVisualLayout.VisualObjectName
            && visual.sprite == door.sprite
            && visual.sharedMaterial != null
            && visual.sharedMaterial.shader != null
            && visual.sharedMaterial.shader.name == DoorVisualMaterial.ShaderName
            && Mathf.Abs(visual.bounds.size.x - DungeonDoorVisualLayout.VisualWidth) <= 0.01f
            && Mathf.Abs(visual.bounds.size.y - DungeonDoorVisualLayout.VisualHeight) <= 0.01f
            && Mathf.Abs(visual.bounds.min.y) <= 0.01f
            && visual.sortingLayerName == DungeonDoorVisualLayout.SortingLayerName
            && visual.sortingOrder == DungeonDoorVisualLayout.SortingOrder
            && wallFootprintCleared
            && directHallwayFootprintCleared
            && hallwayFootprintCleared
            && ceilingTilesPreserved
            && traversalOrderingValid;

        if (!runtimeValid)
        {
            Debug.Log($"Dungeon door runtime visual invalid: placed={placed},door={placedDoor != null},"
                + $"exact={placedDoor != null && placedDoor.GetType() == typeof(Door)},visual={visual != null},"
                + $"visualName={visual?.transform.name},spriteMatch={visual != null && visual.sprite == door.sprite},"
                + $"material={visual?.sharedMaterial?.shader?.name},bounds={visual?.bounds.size},"
                + $"minY={visual?.bounds.min.y},sorting={visual?.sortingLayerName}:{visual?.sortingOrder},"
                + $"wall={wallFootprintCleared},directHall={directHallwayFootprintCleared},"
                + $"hall={hallwayFootprintCleared},ceiling={ceilingTilesPreserved},"
                + $"ordering={traversalOrderingValid}");
        }

        if (placedDoor != null)
        {
            Object.DestroyImmediate(placedDoor.gameObject);
        }

        for (int x = 2; x <= 4; x++)
        {
            BuildableObject placedHallway = grid.GetGridCell(new Vector2Int(x, 0))
                ?.GetOccupant(GridLayer.Hallway) as BuildableObject;
            if (placedHallway != null)
            {
                Object.DestroyImmediate(placedHallway.gameObject);
            }
        }

        Object.DestroyImmediate(wallRoot);
        Object.DestroyImmediate(wallTile);
        Object.DestroyImmediate(floorTile);
        Object.DestroyImmediate(staleHallwayTile);

        return runtimeValid;
    }

    private static bool VerifyInteriorDoorFitsOneCell()
    {
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/InteriorDoor.asset");
        if (door == null
            || !door.IsDoor
            || !door.IsInteriorDoor
            || door.type != typeof(InteriorDoor)
            || door.width != 1
            || door.height != 1
            || !door.unlocked
            || door.tiles == null
            || !door.tiles.TryGetValue(GridTexture.TilemapLayer.BACK, out Tile doorTile)
            || doorTile == null
            || doorTile.sprite == null)
        {
            return false;
        }

        Bounds spriteBounds = doorTile.sprite.bounds;
        Vector3 scale = doorTile.transform.lossyScale;
        Vector2 renderedSize = new Vector2(
            Mathf.Abs(spriteBounds.size.x * scale.x),
            Mathf.Abs(spriteBounds.size.y * scale.y));
        Vector3 renderedCenter = doorTile.transform.MultiplyPoint3x4(spriteBounds.center);
        float renderedRectMinY = renderedCenter.y - renderedSize.y * 0.5f;
        float renderedRectMaxY = renderedCenter.y + renderedSize.y * 0.5f;
        float renderedOpaqueMinY = renderedRectMinY
            + renderedSize.y * InteriorDoorVisualLayout.TransparentBottomRatio;

        return door.GetGridPosList(new Vector2Int(2, 0)).Count == 1
            && door.sprite == doorTile.sprite
            && door.icon == doorTile.sprite
            && doorTile.sprite.name == "env_objects_InteriorDoor"
            && doorTile.sprite.rect.width == 32f
            && doorTile.sprite.rect.height == 64f
            && doorTile.color.a >= 0.99f
            && Mathf.Abs(renderedSize.x - InteriorDoorVisualLayout.VisualWidth) <= 0.01f
            && Mathf.Abs(renderedSize.y - InteriorDoorVisualLayout.VisualHeight) <= 0.01f
            && Mathf.Abs(renderedRectMinY - InteriorDoorVisualLayout.GroundingOffsetY) <= 0.01f
            && Mathf.Abs(renderedRectMaxY - InteriorDoorVisualLayout.OpaqueHeight) <= 0.01f
            && Mathf.Abs(renderedOpaqueMinY) <= 0.01f;
    }

    private static bool VerifyInteriorDoorStaysGroundedInPreservedWall()
    {
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/InteriorDoor.asset");
        if (wall == null || door == null)
        {
            return false;
        }

        Grid grid = new Grid(5, 1);
        GridBuildingPlacementService placement = new GridBuildingPlacementService(
            grid,
            null,
            null,
            new GridBuildingFactory(InjectBuildingDependencies),
            new BuildingPlacementValidator());
        Vector2Int target = new Vector2Int(2, 0);
        bool wallPlaced = placement.TryPlaceBuilding(wall, target, out _);
        bool doorPlaced = placement.TryPlaceBuilding(door, target, out _);
        InteriorDoor placedDoor = grid.GetGridCell(target)?.GetBuildingInlayer(GridLayer.Building) as InteriorDoor;

        GameObject root = new GameObject("DoorWallCompositionTest", typeof(UnityEngine.Grid));
        GameObject tilemapObject = new GameObject("Wall", typeof(Tilemap), typeof(TilemapRenderer));
        tilemapObject.transform.SetParent(root.transform, false);
        Tilemap tilemap = tilemapObject.GetComponent<Tilemap>();
        Tile wallTile = ScriptableObject.CreateInstance<Tile>();
        wallTile.sprite = wall.sprite;
        Tile floorTile = ScriptableObject.CreateInstance<Tile>();
        floorTile.sprite = wall.sprite;

        GridTexture texture = root.AddComponent<GridTexture>();
        texture.wallTilemap = tilemap;
        texture.wall = wallTile;
        texture.floor = floorTile;
        texture.DrawWall(grid);

        SpriteRenderer visual = placedDoor != null ? placedDoor.VisualRenderer : null;
        Bounds visualBounds = visual != null ? visual.bounds : default;
        float opaqueMinY = visualBounds.min.y
            + visualBounds.size.y * InteriorDoorVisualLayout.TransparentBottomRatio;
        bool rendered = wallPlaced
            && doorPlaced
            && placedDoor != null
            && tilemap.GetTile(new Vector3Int(-2, 0, 0)) == wallTile
            && tilemap.GetTile(new Vector3Int(-2, 1, 0)) == wallTile
            && tilemap.GetTile(new Vector3Int(-2, 2, 0)) == wallTile
            && visual != null
            && visual.sprite == door.sprite
            && visual.sprite.name == "env_objects_InteriorDoor"
            && visual.sharedMaterial != null
            && visual.sharedMaterial.shader != null
            && visual.sharedMaterial.shader.name == DoorVisualMaterial.ShaderName
            && Mathf.Abs(visualBounds.size.x - InteriorDoorVisualLayout.VisualWidth) <= 0.01f
            && Mathf.Abs(visualBounds.size.y - InteriorDoorVisualLayout.VisualHeight) <= 0.01f
            && visual.sortingLayerName == InteriorDoorVisualLayout.SortingLayerName
            && visual.sortingOrder == InteriorDoorVisualLayout.SortingOrder
            && Mathf.Abs(opaqueMinY) <= 0.01f
            && Mathf.Abs(visualBounds.max.y - InteriorDoorVisualLayout.OpaqueHeight) <= 0.01f;

        if (placedDoor != null)
        {
            Object.DestroyImmediate(placedDoor.gameObject);
        }

        Object.DestroyImmediate(root);
        Object.DestroyImmediate(wallTile);
        Object.DestroyImmediate(floorTile);
        return rendered;
    }

    private static void InjectBuildingDependencies(BuildableObject building)
    {
        building.ConstructBuildableObject(
            BlueprintResearchWorkService,
            WorldInfoClickSelector,
            FacilityCandidateCacheService,
            RoomFacilityPolicyService);
    }

    private static bool VerifySpriteTileTransform(
        string assetPath,
        float expectedCenterOffsetX,
        float expectedCenterOffsetY,
        float expectedWidth,
        float expectedHeight,
        Vector2 tilemapLocalOffset)
    {
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath);
        if (building == null || building.sprite == null)
        {
            return false;
        }

        Vector2 tileAnchor = GridBuildingTileTransformCalculator.DefaultTileAnchor;
        Matrix4x4 transform = GridBuildingTileTransformCalculator.Calculate(
            building,
            3,
            tileAnchor,
            tilemapLocalOffset);
        Bounds bounds = building.sprite.bounds;
        Vector3 transformedCenter = transform.MultiplyPoint3x4(bounds.center);
        Vector3 transformedScale = transform.lossyScale;
        Vector2 transformedSize = new Vector2(
            Mathf.Abs(bounds.size.x * transformedScale.x),
            Mathf.Abs(bounds.size.y * transformedScale.y));

        Vector2 worldCenterOffset = tileAnchor
            + tilemapLocalOffset
            + (Vector2)transformedCenter;
        Vector2 worldMinOffset = worldCenterOffset - transformedSize * 0.5f;
        Vector2 worldMaxOffset = worldCenterOffset + transformedSize * 0.5f;
        float expectedMinX = expectedCenterOffsetX - expectedWidth * 0.5f;
        float expectedMaxX = expectedCenterOffsetX + expectedWidth * 0.5f;
        float expectedMinY = 0f;
        float expectedMaxY = expectedHeight;

        return Mathf.Abs(worldCenterOffset.x - expectedCenterOffsetX) <= 0.01f
            && Mathf.Abs(worldCenterOffset.y - expectedCenterOffsetY) <= 0.01f
            && Mathf.Abs(transformedSize.x - expectedWidth) <= 0.01f
            && Mathf.Abs(transformedSize.y - expectedHeight) <= 0.01f
            && Mathf.Abs(worldMinOffset.x - expectedMinX) <= 0.01f
            && Mathf.Abs(worldMaxOffset.x - expectedMaxX) <= 0.01f
            && Mathf.Abs(worldMinOffset.y - expectedMinY) <= 0.01f
            && Mathf.Abs(worldMaxOffset.y - expectedMaxY) <= 0.01f;
    }

    private sealed class NoopBlueprintResearchWorkService : IBlueprintResearchWorkService
    {
        public bool HasResearchWorkFor(BuildableObject facility)
        {
            return false;
        }

        public BlueprintResearchWorkResult ApplyResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float seconds)
        {
            return new BlueprintResearchWorkResult(
                false,
                null,
                0f,
                0f,
                1f,
                false,
                "Grid visual fixture has no blueprint research runtime.");
        }
    }

    private sealed class TestGridOccupant : IGridOccupant
    {
        public TestGridOccupant(int gridId)
        {
            GridId = gridId;
        }

        public int GridId { get; }
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => false;
    }

    private sealed class NoopWorldInfoClickSelector : IWorldInfoClickSelector
    {
        public bool TryHandleWorldInfoClick()
        {
            return false;
        }

        public bool TryTriggerCharacterUnderPointer()
        {
            return false;
        }

        public bool TryGetPreferredCharacterUnderPointer(out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacterAtScreenPosition(
            Vector3 screenPosition,
            Camera camera,
            out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacter(Collider2D[] hits, out CharacterActor actor)
        {
            actor = null;
            return false;
        }
    }
}
