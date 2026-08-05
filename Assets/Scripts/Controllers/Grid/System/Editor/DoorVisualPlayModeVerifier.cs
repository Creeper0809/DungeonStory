using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

public static class DoorVisualPlayModeVerifier
{
    [MenuItem("DungeonStory/Debug/Grid Foundation/Run Door Visual PlayMode Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Door visual PlayMode verification requires PlayMode.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<DoorVisualPlayModeVerificationRunner>() != null)
        {
            Debug.LogWarning("Door visual PlayMode verification is already running.");
            return;
        }

        GameObject runnerObject = new GameObject("Door Visual PlayMode Verification Runner");
        runnerObject.AddComponent<DoorVisualPlayModeVerificationRunner>();
    }
}

public sealed class DoorVisualPlayModeVerificationRunner : MonoBehaviour
{
    private const string DungeonDoorCapturePath = "Temp/phase40-dungeon-door-closeup-camera.png";
    private const string GhostCapturePath = "Temp/phase40-interior-door-ghost-camera.png";
    private const string PlacedCapturePath = "Temp/phase40-interior-door-placed-camera.png";
    private const string DungeonDoorCharacterCapturePath =
        "Temp/phase41-dungeon-door-character-camera.png";
    private const string DungeonDoorFrameCapturePath =
        "Temp/phase42-dungeon-door-frame-occlusion-camera.png";
    private const string DungeonDoorInsideCapturePath =
        "Temp/phase45-dungeon-door-inside-front-camera.png";
    private const string InteriorDoorCharacterCapturePath =
        "Temp/phase41-interior-door-character-camera.png";

    private sealed class CharacterTraversalProbe
    {
        public bool ActorFound;
        public bool EnteredExpectedLayer;
        public bool RestoredExpectedLayer;
        public bool ExteriorWallOverlap;
        public bool ExteriorLayerPreserved;
        public bool InsideFrontAfterOpening;
        public bool InsideColliderCleared;
        public bool InsideVisualOverlap;
        public bool InsideLayerFront;
        public int RendererCount;
        public string OverlapLayer = "null";
        public string ExitLayer = "null";
    }

    private sealed class FixedWorldPointer : IWorldPointerPositionProvider
    {
        public Vector3 MouseWorldPosition { get; }

        public FixedWorldPointer(Vector3 mouseWorldPosition)
        {
            MouseWorldPosition = mouseWorldPosition;
        }
    }

    private sealed class NoUiPointerBlocker : IUiPointerBlocker
    {
        public bool IsPointerOverUi()
        {
            return false;
        }
    }

    private sealed class NoOpPlayerInputReader : IPlayerInputReader
    {
        public Vector3 MousePosition => Vector3.zero;
        public float ScreenWidth => Screen.width;
        public float ScreenHeight => Screen.height;
        public float ScrollDeltaY => 0f;
        public bool GetKey(KeyCode keyCode) => false;
        public bool GetKeyDown(KeyCode keyCode) => false;
        public bool GetMouseButton(int button) => false;
        public bool GetMouseButtonDown(int button) => false;
    }

    private IEnumerator Start()
    {
        yield return null;
        yield return null;
        yield return RunVerification();
        Destroy(gameObject);
    }

    private IEnumerator RunVerification()
    {
        LifetimeScope scope = UnityEngine.Object.FindObjectsByType<LifetimeScope>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(item => item != null && item.Container != null);
        DungeonStoryGridBuildingController controller =
            UnityEngine.Object.FindFirstObjectByType<DungeonStoryGridBuildingController>();
        DungeonStoryGridGhostPresenter ghostPresenter =
            UnityEngine.Object.FindFirstObjectByType<DungeonStoryGridGhostPresenter>();
        Camera camera = Camera.main;
        if (scope == null || controller == null || ghostPresenter == null || camera == null)
        {
            throw new InvalidOperationException("Door visual verification could not resolve runtime services.");
        }
        Grid grid = controller.GridSystem.grid;

        Door dungeonDoor = UnityEngine.Object.FindObjectsByType<Door>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(item => item != null
                && item.GetType() == typeof(Door)
                && item.BuildingData != null
                && item.BuildingData.runtimeArchetype == BuildingRuntimeArchetypeKind.Door);
        SpriteRenderer dungeonDoorRenderer = dungeonDoor != null ? dungeonDoor.VisualRenderer : null;
        Bounds dungeonDoorBounds = dungeonDoorRenderer != null ? dungeonDoorRenderer.bounds : default;
        GridTexture gridTexture = UnityEngine.Object.FindFirstObjectByType<GridTexture>();
        int dungeonDoorWallTiles = CountWallTiles(dungeonDoor, gridTexture);
        int dungeonDoorCeilingTiles = CountCeilingTiles(dungeonDoor, gridTexture);
        int dungeonDoorHallwayTiles = CountHallwayTiles(dungeonDoor, gridTexture);
        CharacterSpawner spawner = UnityEngine.Object.FindFirstObjectByType<CharacterSpawner>();
        Vector3 entryDoorWorldPosition = spawner != null
            ? spawner.GetEntryDoorWorldPosition()
            : default;
        Vector2Int entryInsideGridPosition = spawner != null
            ? spawner.GetEntryGridPosition()
            : default;
        Vector3 entryInsideWorldPosition = grid != null
            && grid.IsValidGridPos(entryInsideGridPosition)
                ? grid.GetWorldPos(entryInsideGridPosition)
                : default;
        BoxCollider2D dungeonDoorCollider = dungeonDoor != null
            ? dungeonDoor.GetComponent<BoxCollider2D>()
            : null;
        bool dungeonDataPassed = dungeonDoor != null
            && dungeonDoor.BuildingData.width == 3
            && dungeonDoor.BuildingData.height == 1
            && !dungeonDoor.BuildingData.IsInteriorDoor
            && dungeonDoor.buildPoses != null
            && dungeonDoor.buildPoses.Count == 3;
        bool dungeonVisualPassed = dungeonDoorRenderer != null
            && dungeonDoorRenderer.sprite != null
            && dungeonDoorRenderer.sprite.name == "Door"
            && dungeonDoorRenderer.sharedMaterial != null
            && dungeonDoorRenderer.sharedMaterial.shader != null
            && dungeonDoorRenderer.sharedMaterial.shader.name == DoorVisualMaterial.ShaderName
            && AssetDatabase.GetAssetPath(dungeonDoorRenderer.sprite) == "Assets/Images/Using/Door.png"
            && Mathf.Abs(dungeonDoorBounds.size.x - DungeonDoorVisualLayout.VisualWidth) <= 0.01f
            && Mathf.Abs(dungeonDoorBounds.size.y - DungeonDoorVisualLayout.VisualHeight) <= 0.01f
            && dungeonDoorRenderer.sortingLayerName == DungeonDoorVisualLayout.SortingLayerName
            && dungeonDoorRenderer.sortingOrder == DungeonDoorVisualLayout.SortingOrder
            && dungeonDoor != null
            && dungeonDoor.transform.Find(InteriorDoorVisualLayout.VisualObjectName) == null
            && IsTraversalSortingOrderValid();
        bool dungeonCompositionPassed = dungeonDoor != null
            && dungeonDoorWallTiles == 0
            && dungeonDoorCeilingTiles == dungeonDoor.buildPoses.Count
            && dungeonDoorHallwayTiles == 0;
        bool dungeonColliderPassed = dungeonDoorCollider != null
            && dungeonDoorCollider.isTrigger
            && Vector2.Distance(
                dungeonDoorCollider.size,
                DungeonDoorVisualLayout.TraversalColliderSize) <= 0.01f
            && Vector2.Distance(
                dungeonDoorCollider.offset,
                DungeonDoorVisualLayout.TraversalColliderOffset) <= 0.01f;
        bool dungeonPathPassed = spawner != null
            && dungeonDoor != null
            && Mathf.Abs(entryDoorWorldPosition.x - dungeonDoorBounds.center.x) <= 0.01f
            && Mathf.Abs(entryDoorWorldPosition.y - dungeonDoor.transform.position.y) <= 0.01f
            && Mathf.Abs(entryInsideWorldPosition.y - entryDoorWorldPosition.y) <= 0.01f;
        bool dungeonDoorPassed = dungeonDataPassed
            && dungeonVisualPassed
            && dungeonCompositionPassed
            && dungeonColliderPassed
            && dungeonPathPassed;
        if (dungeonDoorRenderer != null)
        {
            CaptureWorldCloseup(camera, dungeonDoorBounds, DungeonDoorCapturePath);
        }

        CharacterTraversalProbe dungeonTraversal = new CharacterTraversalProbe();
        yield return VerifyCharacterTraversal(
            dungeonDoor,
            dungeonDoorBounds,
            camera,
            DungeonDoorVisualLayout.TraversalSortingLayerName,
            DungeonDoorVisualLayout.DefaultCharacterSortingLayerName,
            DungeonDoorCharacterCapturePath,
            DungeonDoorFrameCapturePath,
            dungeonTraversal);

        PressVisibleButton("건축");
        yield return null;
        if (FindVisibleButton("벽/문") == null)
        {
            PressVisibleButton("건축");
            yield return null;
        }
        PressVisibleButton("벽/문");
        yield return null;
        PressVisibleButton("내벽");
        yield return null;

        Vector2Int target = FindWallTarget(controller, grid);
        Vector3 cellFloor = grid.GetWorldPos(target);
        FixedWorldPointer pointer = new FixedWorldPointer(cellFloor + new Vector3(0f, 1.5f, 0f));
        ReconstructRuntimeInput(scope, controller, ghostPresenter, pointer);

        BuildableObject beforeWall = grid.GetGridCell(target).GetBuildingInlayer(GridLayer.Building);
        bool wallBuildable = controller.IsBuildableAt(target);
        controller.TriggerPlaceBuilding();
        bool wallDragStarted = controller.GridSystem.isDragging;
        if (wallDragStarted)
        {
            controller.TriggerPlaceBuilding();
        }
        yield return null;
        yield return new WaitForEndOfFrame();

        BuildableObject placedWall = grid.GetGridCell(target).GetBuildingInlayer(GridLayer.Building);
        bool wallWasPlaced = placedWall != null;
        bool wallWasStructural = placedWall != null
            && placedWall.BuildingData != null
            && placedWall.BuildingData.IsStructuralWall;
        int wallTilesAfterWall = CountWallTiles(target);

        PressVisibleButton("건축");
        yield return null;
        if (FindVisibleButton("벽/문") == null)
        {
            PressVisibleButton("건축");
            yield return null;
        }
        PressVisibleButton("벽/문");
        yield return null;
        PressVisibleButton("내벽 문");
        yield return null;
        yield return new WaitForEndOfFrame();

        SpriteRenderer ghostRenderer = FindActiveGhostRenderer();
        Bounds ghostBounds = ghostRenderer != null ? ghostRenderer.bounds : default;
        string ghostSpriteName = GetSpriteName(ghostRenderer);
        bool ghostShowsDoor = ghostSpriteName == "env_objects_InteriorDoor";
        float ghostOpaqueBottom = GetOpaqueBottom(ghostBounds);
        int wallTilesWithGhost = CountWallTiles(target);
        CaptureCamera(camera, GhostCapturePath);

        BuildableObject beforeDoor = grid.GetGridCell(target).GetBuildingInlayer(GridLayer.Building);
        bool doorWillReplacePlacedWall = beforeDoor == placedWall;
        bool doorBuildable = controller.IsBuildableAt(target);
        controller.TriggerPlaceBuilding();
        yield return null;
        yield return new WaitForEndOfFrame();

        InteriorDoor placedDoor = grid.GetGridCell(target).GetBuildingInlayer(GridLayer.Building) as InteriorDoor;
        SpriteRenderer doorRenderer = placedDoor != null ? placedDoor.VisualRenderer : null;
        Bounds doorBounds = doorRenderer != null ? doorRenderer.bounds : default;
        float doorOpaqueBottom = GetOpaqueBottom(doorBounds);
        int wallTilesAfterDoor = CountWallTiles(target);
        BoxCollider2D doorCollider = placedDoor != null ? placedDoor.GetComponent<BoxCollider2D>() : null;
        CaptureCamera(camera, PlacedCapturePath);

        CharacterTraversalProbe interiorTraversal = new CharacterTraversalProbe();
        yield return VerifyCharacterTraversal(
            placedDoor,
            doorBounds,
            camera,
            DungeonDoorVisualLayout.DefaultCharacterSortingLayerName,
            DungeonDoorVisualLayout.DefaultCharacterSortingLayerName,
            InteriorDoorCharacterCapturePath,
            null,
            interiorTraversal);

        bool dungeonTraversalPassed = dungeonTraversal.ActorFound
            && dungeonTraversal.EnteredExpectedLayer
            && dungeonTraversal.ExteriorWallOverlap
            && dungeonTraversal.ExteriorLayerPreserved
            && dungeonTraversal.InsideFrontAfterOpening
            && dungeonTraversal.RestoredExpectedLayer;
        bool placementFlowPassed = beforeWall == null
            && wallBuildable
            && wallWasPlaced
            && wallWasStructural
            && wallTilesAfterWall == 3
            && ghostShowsDoor
            && Mathf.Abs(ghostBounds.size.x - InteriorDoorVisualLayout.VisualWidth) <= 0.01f
            && Mathf.Abs(ghostBounds.size.y - InteriorDoorVisualLayout.VisualHeight) <= 0.01f
            && Mathf.Abs(ghostOpaqueBottom - cellFloor.y) <= 0.01f
            && Mathf.Abs(ghostBounds.max.y - (cellFloor.y + InteriorDoorVisualLayout.OpaqueHeight)) <= 0.01f
            && wallTilesWithGhost == 3
            && doorWillReplacePlacedWall
            && doorBuildable;
        bool interiorDoorPassed = placedDoor != null
            && doorRenderer != null
            && doorRenderer.sprite != null
            && doorRenderer.sprite.name == "env_objects_InteriorDoor"
            && doorRenderer.sharedMaterial != null
            && doorRenderer.sharedMaterial.shader != null
            && doorRenderer.sharedMaterial.shader.name == DoorVisualMaterial.ShaderName
            && Mathf.Abs(doorBounds.size.x - InteriorDoorVisualLayout.VisualWidth) <= 0.01f
            && Mathf.Abs(doorBounds.size.y - InteriorDoorVisualLayout.VisualHeight) <= 0.01f
            && doorRenderer.sortingLayerName == InteriorDoorVisualLayout.SortingLayerName
            && doorRenderer.sortingOrder == InteriorDoorVisualLayout.SortingOrder
            && Mathf.Abs(doorOpaqueBottom - cellFloor.y) <= 0.01f
            && Mathf.Abs(doorBounds.max.y - (cellFloor.y + InteriorDoorVisualLayout.OpaqueHeight)) <= 0.01f
            && wallTilesAfterDoor == 3
            && doorCollider != null
            && doorCollider.isTrigger
            && Mathf.Abs(doorCollider.size.x - 1f) <= 0.01f
            && interiorTraversal.ActorFound
            && interiorTraversal.EnteredExpectedLayer
            && interiorTraversal.RestoredExpectedLayer
            && controller.GridSystem.Mode == GridMode.None;
        bool passed = dungeonDoorPassed
            && dungeonTraversalPassed
            && placementFlowPassed
            && interiorDoorPassed;

        string report = $"dungeonDoor={dungeonDoor?.GetType().Name ?? "null"}; "
            + $"dungeonSprite={GetSpriteName(dungeonDoorRenderer)}; "
            + $"dungeonBounds={FormatBounds(dungeonDoorBounds)}; "
            + $"dungeonWallTiles={dungeonDoorWallTiles}; "
            + $"dungeonCeilingTiles={dungeonDoorCeilingTiles}; "
            + $"dungeonHallwayTiles={dungeonDoorHallwayTiles}; "
            + $"dataValues={FormatDungeonData(dungeonDoor)}; "
            + $"groups=dungeonStatic:{dungeonDoorPassed},"
            + $"data:{dungeonDataPassed},visual:{dungeonVisualPassed},"
            + $"composition:{dungeonCompositionPassed},collider:{dungeonColliderPassed},"
            + $"path:{dungeonPathPassed},"
            + $"dungeonTraversal:{dungeonTraversalPassed},"
            + $"placement:{placementFlowPassed},interior:{interiorDoorPassed}; "
            + $"entryPath={spawner?.GetOutsideSpawnWorldPosition().ToString() ?? "null"}"
            + $"->{entryDoorWorldPosition}->{entryInsideWorldPosition}; "
            + $"dungeonTraversal={FormatTraversal(dungeonTraversal)}; "
            + $"target={target}; wallBuildable={wallBuildable}; "
            + $"wallDragStarted={wallDragStarted}; "
            + $"wallTiles={wallTilesAfterWall}/{wallTilesWithGhost}/{wallTilesAfterDoor}; "
            + $"ghostSprite={ghostSpriteName}; ghostBounds={FormatBounds(ghostBounds)}; "
            + $"ghostOpaqueBottom={ghostOpaqueBottom:F2}; doorBuildable={doorBuildable}; "
            + $"occupant={placedDoor?.GetType().Name ?? "null"}; "
            + $"doorSprite={GetSpriteName(doorRenderer)}; doorBounds={FormatBounds(doorBounds)}; "
            + $"doorOpaqueBottom={doorOpaqueBottom:F2}; collider={FormatCollider(doorCollider)}; "
            + $"interiorTraversal={FormatTraversal(interiorTraversal)}; "
            + $"mode={controller.GridSystem.Mode}; "
            + $"captures={DungeonDoorCapturePath},{DungeonDoorCharacterCapturePath},"
            + $"{DungeonDoorFrameCapturePath},{DungeonDoorInsideCapturePath},"
            + $"{GhostCapturePath},{PlacedCapturePath},{InteriorDoorCharacterCapturePath}";

        if (passed)
        {
            Debug.Log("Door visual PlayMode verification passed. " + report);
        }
        else
        {
            Debug.LogError("Door visual PlayMode verification failed. " + report);
        }
    }

    private static IEnumerator VerifyCharacterTraversal(
        Door door,
        Bounds doorBounds,
        Camera camera,
        string expectedOverlapLayer,
        string expectedExitLayer,
        string capturePath,
        string frameOcclusionCapturePath,
        CharacterTraversalProbe result)
    {
        GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/CharacterPrefab.prefab");
        BoxCollider2D doorCollider = door != null ? door.GetComponent<BoxCollider2D>() : null;
        if (characterPrefab == null || doorCollider == null || !doorCollider.isTrigger)
        {
            yield break;
        }

        GameObject actorObject = UnityEngine.Object.Instantiate(characterPrefab);
        actorObject.name = "Door Traversal Verification Character";
        CharacterActor actor = actorObject.GetComponent<CharacterActor>();
        Collider2D actorCollider = actorObject.GetComponent<Collider2D>();
        Rigidbody2D actorBody = actorObject.GetComponent<Rigidbody2D>();
        SpriteRenderer[] actorRenderers = actorObject.GetComponentsInChildren<SpriteRenderer>(true);
        if (actor == null
            || actorCollider == null
            || actorBody == null
            || actorRenderers.Length == 0
            || !actorObject.CompareTag("Character"))
        {
            UnityEngine.Object.Destroy(actorObject);
            yield break;
        }

        result.ActorFound = true;
        result.RendererCount = actorRenderers.Length;
        foreach (SpriteRenderer renderer in actorRenderers)
        {
            DoorVisualMaterial.Apply(
                renderer,
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Resources/Materials/DoorSpriteUnlit.mat"));
        }

        foreach (MonoBehaviour behaviour in actorObject.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
        }

        actorBody.linearVelocity = Vector2.zero;
        actorBody.angularVelocity = 0f;
        actorBody.constraints = RigidbodyConstraints2D.FreezeAll;
        actor.ChangeLayer(DungeonDoorVisualLayout.DefaultCharacterSortingLayerName);

        Vector2 actorPosition = actorBody.position;
        Vector2 actorCenterOffset = (Vector2)actorCollider.bounds.center - actorPosition;
        float actorFootOffset = actorCollider.bounds.min.y - actorPosition.y;
        actorBody.position = new Vector2(
            doorCollider.bounds.center.x - actorCenterOffset.x,
            door.transform.position.y - actorFootOffset);
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        result.OverlapLayer = GetSortingLayers(actorRenderers);
        result.EnteredExpectedLayer = actorRenderers.All(
            renderer => renderer.sortingLayerName == expectedOverlapLayer);
        Bounds captureBounds = doorBounds;
        foreach (SpriteRenderer renderer in actorRenderers)
        {
            captureBounds.Encapsulate(renderer.bounds);
        }
        CaptureWorldCloseup(camera, captureBounds, capturePath);

        if (!string.IsNullOrEmpty(frameOcclusionCapturePath))
        {
            actorBody.position = new Vector2(
                doorBounds.max.x
                    + actorCollider.bounds.extents.x
                    - 0.05f
                    - actorCenterOffset.x,
                door.transform.position.y - actorFootOffset);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForEndOfFrame();
            result.ExteriorWallOverlap = doorCollider.bounds.Intersects(actorCollider.bounds)
                && actorRenderers.Any(renderer => doorBounds.Intersects(renderer.bounds));
            result.ExteriorLayerPreserved = actorRenderers.All(
                renderer => renderer.sortingLayerName == expectedOverlapLayer);
            CaptureWorldCloseup(camera, doorBounds, frameOcclusionCapturePath);

            actorBody.position = new Vector2(
                doorCollider.bounds.min.x
                    - actorCollider.bounds.extents.x
                    - 0.5f
                    - actorCenterOffset.x,
                door.transform.position.y - actorFootOffset);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForEndOfFrame();
            result.InsideColliderCleared = !doorCollider.bounds.Intersects(actorCollider.bounds);
            result.InsideVisualOverlap = actorRenderers.Any(
                renderer => doorBounds.Intersects(renderer.bounds));
            result.InsideLayerFront = actorRenderers.All(
                renderer => renderer.sortingLayerName == expectedExitLayer);
            result.InsideFrontAfterOpening = result.InsideColliderCleared
                && result.InsideVisualOverlap
                && result.InsideLayerFront;
            CaptureWorldCloseup(camera, doorBounds, DungeonDoorInsideCapturePath);
        }

        actorBody.position = new Vector2(
            doorCollider.bounds.max.x + actorCollider.bounds.extents.x + 5f,
            door.transform.position.y - actorFootOffset);
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        result.ExitLayer = GetSortingLayers(actorRenderers);
        result.RestoredExpectedLayer = actorRenderers.All(
            renderer => renderer.sortingLayerName == expectedExitLayer);
        UnityEngine.Object.Destroy(actorObject);
        yield return null;
    }

    private static void ReconstructRuntimeInput(
        LifetimeScope scope,
        DungeonStoryGridBuildingController controller,
        DungeonStoryGridGhostPresenter ghostPresenter,
        IWorldPointerPositionProvider pointer)
    {
        controller.Construct(
            scope.Container.Resolve<IGridSystemProvider>(),
            scope.Container.Resolve<IDataCatalog>(),
            pointer,
            scope.Container.Resolve<IGridTextureProvider>(),
            scope.Container.Resolve<IObjectResolver>(),
            scope.Container.Resolve<IGameSessionStateProvider>(),
            scope.Container.Resolve<IGameMoneyAccount>(),
            scope.Container.Resolve<ProgressionSceneRuntimeReferences>(),
            scope.Container.Resolve<IGridBuildingObjectFactory>(),
            new NoUiPointerBlocker(),
            new NoOpPlayerInputReader(),
            scope.Container.Resolve<IWorkOrderRuntime>(),
            scope.Container.Resolve<IGameEventBus>(),
            scope.Container.Resolve<IGameClock>(),
            scope.Container.Resolve<IDungeonDebugRuleQuery>());
        ghostPresenter.Construct(
            scope.Container.Resolve<IGridSystemProvider>(),
            scope.Container.Resolve<IDungeonGridBuildingControllerProvider>(),
            pointer,
            scope.Container.Resolve<IGridGhostObjectResolver>());
    }

    private static Vector2Int FindWallTarget(DungeonStoryGridBuildingController controller, Grid grid)
    {
        Vector2Int preferred = new Vector2Int(22, 0);
        if (CanUseTarget(controller, grid, preferred))
        {
            return preferred;
        }

        for (int x = 1; x < grid.width - 1; x++)
        {
            Vector2Int candidate = new Vector2Int(x, 0);
            if (CanUseTarget(controller, grid, candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Door visual verification found no buildable wall target.");
    }

    private static bool CanUseTarget(
        DungeonStoryGridBuildingController controller,
        Grid grid,
        Vector2Int target)
    {
        return grid.IsValidGridPos(target)
            && grid.GetGridCell(target).GetBuildingInlayer(GridLayer.Building) == null
            && controller.IsBuildableAt(target);
    }

    private static Button FindVisibleButton(string label)
    {
        return UnityEngine.Object.FindObjectsByType<Button>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(button =>
            {
                TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
                return button != null
                    && button.gameObject.activeInHierarchy
                    && button.interactable
                    && text != null
                    && text.text == label;
            });
    }

    private static void PressVisibleButton(string label)
    {
        Button button = FindVisibleButton(label);
        if (button == null)
        {
            throw new InvalidOperationException($"Visible '{label}' button was not found.");
        }

        RectTransform rect = button.transform as RectTransform;
        Vector2 center = rect != null
            ? (Vector2)rect.TransformPoint(rect.rect.center)
            : (Vector2)button.transform.position;
        button.OnPointerClick(new PointerEventData(EventSystem.current)
        {
            position = center,
            button = PointerEventData.InputButton.Left
        });
        Canvas.ForceUpdateCanvases();
        Debug.Log($"Door visual verifier pressed '{label}' at {center}.");
    }

    private static SpriteRenderer FindActiveGhostRenderer()
    {
        GridGhostObject ghost = UnityEngine.Object.FindFirstObjectByType<GridGhostObject>();
        return ghost != null
            ? ghost.GetComponentsInChildren<SpriteRenderer>(true)
                .FirstOrDefault(renderer => renderer.gameObject.activeInHierarchy && renderer.sprite != null)
            : null;
    }

    private static int CountWallTiles(Vector2Int target)
    {
        Tilemap wallMap = UnityEngine.Object.FindObjectsByType<Tilemap>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(item => item.name == "Wall");
        if (wallMap == null)
        {
            return 0;
        }

        int count = 0;
        int baseY = target.y * GridBuildingTileTransformCalculator.DefaultCellTileHeight;
        for (int y = 0; y < GridBuildingTileTransformCalculator.DefaultCellTileHeight; y++)
        {
            if (wallMap.HasTile(new Vector3Int(-target.x, baseY + y, 0)))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountWallTiles(Door door, GridTexture texture)
    {
        if (door == null
            || door.buildPoses == null
            || texture == null
            || texture.wallTilemap == null
            || texture.wall == null)
        {
            return -1;
        }

        int count = 0;
        foreach (Vector2Int position in door.buildPoses)
        {
            int baseY = position.y * GridBuildingTileTransformCalculator.DefaultCellTileHeight;
            for (int y = 0; y < GridBuildingTileTransformCalculator.DefaultCellTileHeight; y++)
            {
                if (texture.wallTilemap.GetTile(new Vector3Int(-position.x, baseY + y, 0)) == texture.wall)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int CountCeilingTiles(Door door, GridTexture texture)
    {
        if (door == null
            || door.buildPoses == null
            || texture == null
            || texture.wallTilemap == null
            || texture.floor == null)
        {
            return -1;
        }

        int count = 0;
        foreach (Vector2Int position in door.buildPoses)
        {
            Vector3Int tilePosition = new Vector3Int(
                -position.x,
                position.y * GridBuildingTileTransformCalculator.DefaultCellTileHeight
                    + GridBuildingTileTransformCalculator.DefaultCellTileHeight - 1,
                0);
            if (texture.wallTilemap.GetTile(tilePosition) == texture.floor)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountHallwayTiles(Door door, GridTexture texture)
    {
        if (door == null || door.buildPoses == null || texture == null)
        {
            return -1;
        }

        HashSet<Tilemap> hallwayTilemaps = new HashSet<Tilemap>();
        AddHallwayTilemap(texture.buildingTilemapEven, hallwayTilemaps);
        AddHallwayTilemap(texture.buildingTilemapOdd, hallwayTilemaps);
        if (hallwayTilemaps.Count == 0)
        {
            return -1;
        }

        int count = 0;
        foreach (Tilemap hallwayTilemap in hallwayTilemaps)
        {
            foreach (Vector2Int position in door.buildPoses)
            {
                Vector3Int tilePosition = new Vector3Int(
                    -position.x,
                    position.y * GridBuildingTileTransformCalculator.DefaultCellTileHeight,
                    0);
                if (hallwayTilemap.HasTile(tilePosition))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static void AddHallwayTilemap(
        Dictionary<GridTexture.TilemapLayer, Tilemap> tilemaps,
        HashSet<Tilemap> result)
    {
        if (tilemaps != null
            && tilemaps.TryGetValue(GridTexture.TilemapLayer.HALLWAY, out Tilemap hallwayTilemap)
            && hallwayTilemap != null)
        {
            result.Add(hallwayTilemap);
        }
    }

    private static float GetOpaqueBottom(Bounds bounds)
    {
        return bounds.min.y + bounds.size.y * InteriorDoorVisualLayout.TransparentBottomRatio;
    }

    private static bool IsTraversalSortingOrderValid()
    {
        int doorLayer = SortingLayer.GetLayerValueFromName(DungeonDoorVisualLayout.SortingLayerName);
        int traversalLayer = SortingLayer.GetLayerValueFromName(
            DungeonDoorVisualLayout.TraversalSortingLayerName);
        int ceilingLayer = SortingLayer.GetLayerValueFromName(
            DungeonDoorVisualLayout.CeilingSortingLayerName);
        int defaultLayer = SortingLayer.GetLayerValueFromName(
            DungeonDoorVisualLayout.DefaultCharacterSortingLayerName);
        return traversalLayer < doorLayer
            && doorLayer == ceilingLayer
            && DungeonDoorVisualLayout.SortingOrder < DungeonDoorVisualLayout.CeilingSortingOrder
            && ceilingLayer < defaultLayer;
    }

    private static string GetSortingLayers(IEnumerable<SpriteRenderer> renderers)
    {
        string[] layers = renderers
            .Where(renderer => renderer != null)
            .Select(renderer => renderer.sortingLayerName)
            .Distinct()
            .ToArray();
        return layers.Length > 0 ? string.Join("|", layers) : "null";
    }

    private static void CaptureCamera(Camera camera, string relativePath)
    {
        CaptureCamera(camera, relativePath, Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
    }

    private static void CaptureWorldCloseup(Camera sourceCamera, Bounds bounds, string relativePath)
    {
        GameObject cameraObject = new GameObject("Door Visual Closeup Camera");
        Camera closeupCamera = cameraObject.AddComponent<Camera>();
        closeupCamera.CopyFrom(sourceCamera);
        closeupCamera.enabled = false;
        closeupCamera.orthographic = true;
        closeupCamera.orthographicSize = Mathf.Max(2.25f, bounds.extents.y + 0.75f);
        closeupCamera.aspect = 1f;
        closeupCamera.ResetProjectionMatrix();
        closeupCamera.transform.SetPositionAndRotation(
            new Vector3(bounds.center.x, bounds.center.y, sourceCamera.transform.position.z),
            sourceCamera.transform.rotation);
        closeupCamera.ResetWorldToCameraMatrix();

        try
        {
            CaptureCamera(closeupCamera, relativePath, 768, 768);
        }
        finally
        {
            UnityEngine.Object.Destroy(cameraObject);
        }
    }

    private static void CaptureCamera(Camera camera, string relativePath, int width, int height)
    {
        Directory.CreateDirectory("Temp");
        RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D capture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            capture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            capture.Apply();
            File.WriteAllBytes(relativePath, capture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            renderTexture.Release();
            UnityEngine.Object.Destroy(renderTexture);
            UnityEngine.Object.Destroy(capture);
        }
    }

    private static string GetSpriteName(SpriteRenderer renderer)
    {
        return renderer != null && renderer.sprite != null ? renderer.sprite.name : "null";
    }

    private static string FormatBounds(Bounds bounds)
    {
        return $"min={bounds.min:F2},max={bounds.max:F2},size={bounds.size:F2}";
    }

    private static string FormatCollider(BoxCollider2D collider)
    {
        return collider != null
            ? $"size={collider.size},offset={collider.offset},trigger={collider.isTrigger}"
            : "null";
    }

    private static string FormatTraversal(CharacterTraversalProbe probe)
    {
        return probe != null
            ? $"actor={probe.ActorFound},renderers={probe.RendererCount},"
                + $"overlap={probe.OverlapLayer}/{probe.EnteredExpectedLayer},"
                + $"exteriorWall={probe.ExteriorWallOverlap}/{probe.ExteriorLayerPreserved},"
                + $"insideFront={probe.InsideFrontAfterOpening}"
                + $"(collider={probe.InsideColliderCleared},visual={probe.InsideVisualOverlap},"
                + $"layer={probe.InsideLayerFront}),"
                + $"exit={probe.ExitLayer}/{probe.RestoredExpectedLayer}"
            : "null";
    }

    private static string FormatDungeonData(Door door)
    {
        if (door == null || door.BuildingData == null)
        {
            return "null";
        }

        int buildPositionCount = door.buildPoses != null ? door.buildPoses.Count : -1;
        return $"size={door.BuildingData.width}x{door.BuildingData.height},"
            + $"unlocked={door.BuildingData.unlocked},"
            + $"interior={door.BuildingData.IsInteriorDoor},poses={buildPositionCount}";
    }
}
