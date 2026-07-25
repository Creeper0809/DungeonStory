using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using VContainer;

public class DungeonSceneBackdropFitter : MonoBehaviour
{
    [SerializeField] private Transform backgroundRoot;
    [SerializeField] private SpriteRenderer solidBackground;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Vector2Int horizontalPaddingTiles = new Vector2Int(8, 8);
    [SerializeField, Min(0f)] private float cameraViewportPadding = 2f;

    private IGridSystemProvider gridSystemProvider;
    private IDungeonBackdropSpriteTilingFactory spriteTilingFactory;
    private IMainCameraProvider mainCameraProvider;
    private GridSystemManager gridSystemManager;
    private int fittedLeftTile;
    private int fittedRightTile;
    private Vector3 lastCameraPosition;
    private float lastOrthographicSize = float.NaN;
    private float lastAspect = float.NaN;

    [Inject]
    public void Construct(
        IGridSystemProvider gridSystemProvider,
        IDungeonBackdropSpriteTilingFactory spriteTilingFactory,
        IMainCameraProvider mainCameraProvider)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.spriteTilingFactory = spriteTilingFactory ?? throw new ArgumentNullException(nameof(spriteTilingFactory));
        this.mainCameraProvider = mainCameraProvider ?? throw new ArgumentNullException(nameof(mainCameraProvider));
        SubscribeToGridExpansionIfInjected();
    }

    private void OnEnable()
    {
        SubscribeToGridExpansionIfInjected();
    }

    private void Start()
    {
        SubscribeToGridExpansion();
        FitToGrid();
    }

    private void OnDisable()
    {
        UnsubscribeFromGridExpansion();
    }

    private void LateUpdate()
    {
        Camera camera = mainCameraProvider?.Camera;
        if (camera == null || !HasCameraViewportChanged(camera))
        {
            return;
        }

        FitSolidBackground(fittedLeftTile, fittedRightTile, camera);
        RememberCameraViewport(camera);
    }

    public void FitToGrid()
    {
        Grid grid = RequireGridSystemProvider().Grid;
        int width = grid.width;
        if (width <= 0)
        {
            throw new InvalidOperationException($"{nameof(DungeonSceneBackdropFitter)} received an invalid grid width.");
        }

        int leftTile = -width - Mathf.Max(0, horizontalPaddingTiles.x);
        int rightTile = Mathf.Max(0, horizontalPaddingTiles.y);
        fittedLeftTile = leftTile;
        fittedRightTile = rightTile;
        ExtendGround(leftTile, rightTile);
        ExtendBackgroundSprites(leftTile, rightTile);
        Camera camera = mainCameraProvider?.Camera;
        FitSolidBackground(leftTile, rightTile, camera);
        RememberCameraViewport(camera);
    }

    private void SubscribeToGridExpansionIfInjected()
    {
        if (gridSystemProvider != null && isActiveAndEnabled)
        {
            SubscribeToGridExpansion();
        }
    }

    private void SubscribeToGridExpansion()
    {
        GridSystemManager manager = RequireGridSystemProvider().Manager;
        if (gridSystemManager == manager)
        {
            return;
        }

        UnsubscribeFromGridExpansion();
        gridSystemManager = manager;
        gridSystemManager.OnGridExpand += FitToGrid;
    }

    private void UnsubscribeFromGridExpansion()
    {
        if (gridSystemManager == null)
        {
            return;
        }

        gridSystemManager.OnGridExpand -= FitToGrid;
        gridSystemManager = null;
    }

    private IGridSystemProvider RequireGridSystemProvider()
    {
        return gridSystemProvider
            ?? throw new InvalidOperationException($"{nameof(DungeonSceneBackdropFitter)} requires {nameof(IGridSystemProvider)} injection.");
    }

    private IDungeonBackdropSpriteTilingFactory RequireSpriteTilingFactory()
    {
        return spriteTilingFactory
            ?? throw new InvalidOperationException($"{nameof(DungeonSceneBackdropFitter)} requires {nameof(IDungeonBackdropSpriteTilingFactory)} injection.");
    }

    private Transform ResolveBackgroundRoot()
    {
        return backgroundRoot != null
            ? backgroundRoot
            : throw new InvalidOperationException(
                $"{nameof(DungeonSceneBackdropFitter)} on '{name}' requires a serialized {nameof(backgroundRoot)} reference.");
    }

    private Tilemap ResolveGroundTilemap()
    {
        return groundTilemap != null
            ? groundTilemap
            : throw new InvalidOperationException(
                $"{nameof(DungeonSceneBackdropFitter)} on '{name}' requires a serialized {nameof(groundTilemap)} reference.");
    }

    private SpriteRenderer ResolveSolidBackground()
    {
        return solidBackground != null
            ? solidBackground
            : throw new InvalidOperationException(
                $"{nameof(DungeonSceneBackdropFitter)} on '{name}' requires a serialized {nameof(solidBackground)} reference.");
    }

    private void ExtendGround(int leftTile, int rightTile)
    {
        Tilemap targetTilemap = ResolveGroundTilemap();

        Dictionary<int, TileBase> sampleByY = new Dictionary<int, TileBase>();
        BoundsInt bounds = targetTilemap.cellBounds;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                TileBase tile = targetTilemap.GetTile(new Vector3Int(x, y, 0));
                if (tile == null) continue;

                sampleByY[y] = tile;
                break;
            }
        }

        foreach (KeyValuePair<int, TileBase> pair in sampleByY)
        {
            for (int x = leftTile; x <= rightTile; x++)
            {
                Vector3Int position = new Vector3Int(x, pair.Key, 0);
                if (targetTilemap.GetTile(position) == null)
                {
                    targetTilemap.SetTile(position, pair.Value);
                }
            }
        }
    }

    private void ExtendBackgroundSprites(int leftTile, int rightTile)
    {
        Transform targetBackgroundRoot = ResolveBackgroundRoot();
        SpriteRenderer solid = ResolveSolidBackground();

        Dictionary<string, List<SpriteRenderer>> renderersBySprite = new Dictionary<string, List<SpriteRenderer>>();
        foreach (SpriteRenderer renderer in targetBackgroundRoot.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == solid || !IsTiledBackgroundRenderer(renderer)) continue;

            string spriteName = renderer.sprite.name;
            if (!renderersBySprite.TryGetValue(spriteName, out List<SpriteRenderer> renderers))
            {
                renderers = new List<SpriteRenderer>();
                renderersBySprite[spriteName] = renderers;
            }

            renderers.Add(renderer);
        }

        foreach (List<SpriteRenderer> renderers in renderersBySprite.Values)
        {
            ExtendBackgroundSpriteGroup(renderers, leftTile, rightTile);
        }
    }

    private static bool IsTiledBackgroundRenderer(SpriteRenderer renderer)
    {
        return renderer != null && renderer.sprite != null;
    }

    private void ExtendBackgroundSpriteGroup(List<SpriteRenderer> renderers, int leftTile, int rightTile)
    {
        if (renderers == null || renderers.Count == 0) return;

        renderers.Sort((a, b) => a.bounds.min.x.CompareTo(b.bounds.min.x));
        SpriteRenderer template = renderers[0];
        float tileWidth = template.bounds.size.x;
        if (tileWidth <= 0.01f) return;

        float currentMin = GetMinX(renderers);
        while (currentMin > leftTile)
        {
            SpriteRenderer copy = RequireSpriteTilingFactory().Duplicate(template, currentMin - tileWidth);
            renderers.Add(copy);
            currentMin -= tileWidth;
        }

        float currentMax = GetMaxX(renderers);
        while (currentMax < rightTile)
        {
            SpriteRenderer copy = RequireSpriteTilingFactory().Duplicate(template, currentMax);
            renderers.Add(copy);
            currentMax += tileWidth;
        }
    }

    private void FitSolidBackground(int leftTile, int rightTile, Camera camera)
    {
        SpriteRenderer renderer = ResolveSolidBackground();
        Grid grid = RequireGridSystemProvider().Grid;
        Rect coverage = CalculateCoverageRect(
            leftTile,
            rightTile,
            grid.OriginPosition.y,
            grid.OriginPosition.y + grid.height * grid.CellWorldHeight,
            camera,
            cameraViewportPadding);

        Bounds currentBounds = renderer.bounds;
        if (currentBounds.size.x <= 0.001f || currentBounds.size.y <= 0.001f)
        {
            return;
        }

        Vector3 scale = renderer.transform.localScale;
        scale.x *= coverage.width / currentBounds.size.x;
        scale.y *= coverage.height / currentBounds.size.y;
        renderer.transform.localScale = scale;

        Vector3 position = renderer.transform.position;
        position.x = coverage.center.x;
        position.y = coverage.center.y;
        renderer.transform.position = position;
    }

    public static Rect CalculateCoverageRect(
        float worldMinX,
        float worldMaxX,
        float worldMinY,
        float worldMaxY,
        Camera camera,
        float padding)
    {
        float safePadding = Mathf.Max(0f, padding);
        float minX = Mathf.Min(worldMinX, worldMaxX) - safePadding;
        float maxX = Mathf.Max(worldMinX, worldMaxX) + safePadding;
        float minY = Mathf.Min(worldMinY, worldMaxY) - safePadding;
        float maxY = Mathf.Max(worldMinY, worldMaxY) + safePadding;
        if (camera != null && camera.orthographic)
        {
            float halfHeight = Mathf.Max(0.01f, camera.orthographicSize);
            float halfWidth = halfHeight * Mathf.Max(0.01f, camera.aspect);
            minX = Mathf.Min(minX, camera.transform.position.x - halfWidth - safePadding);
            maxX = Mathf.Max(maxX, camera.transform.position.x + halfWidth + safePadding);
            minY = Mathf.Min(minY, camera.transform.position.y - halfHeight - safePadding);
            maxY = Mathf.Max(maxY, camera.transform.position.y + halfHeight + safePadding);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private bool HasCameraViewportChanged(Camera camera)
    {
        return camera != null
            && ((camera.transform.position - lastCameraPosition).sqrMagnitude > 0.0001f
                || !Mathf.Approximately(camera.orthographicSize, lastOrthographicSize)
                || !Mathf.Approximately(camera.aspect, lastAspect));
    }

    private void RememberCameraViewport(Camera camera)
    {
        if (camera == null)
        {
            return;
        }

        lastCameraPosition = camera.transform.position;
        lastOrthographicSize = camera.orthographicSize;
        lastAspect = camera.aspect;
    }

    private static float GetMinX(IEnumerable<SpriteRenderer> renderers)
    {
        float min = float.PositiveInfinity;
        foreach (SpriteRenderer renderer in renderers)
        {
            min = Mathf.Min(min, renderer.bounds.min.x);
        }

        return min;
    }

    private static float GetMaxX(IEnumerable<SpriteRenderer> renderers)
    {
        float max = float.NegativeInfinity;
        foreach (SpriteRenderer renderer in renderers)
        {
            max = Mathf.Max(max, renderer.bounds.max.x);
        }

        return max;
    }
}
