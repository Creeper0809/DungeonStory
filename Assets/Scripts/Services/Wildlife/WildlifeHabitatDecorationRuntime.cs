using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class WildlifeHabitatDecorationRuntime : IDisposable
{
    private const string RootName = "WildlifeHabitatDecorations";
    private const string SortingLayerName = "OutsideObject";
    private const int TreeSortingOrder = 4;
    private const int RockSortingOrder = 18;
    private const int FlowerSortingOrder = 26;
    private const float FlowerDepletedThreshold = 0.06f;

    private sealed class FlowerPatchVisual
    {
        public WildlifeHabitatPatch Patch;
        public readonly List<SpriteRenderer> Renderers = new List<SpriteRenderer>();
    }

    private readonly Dictionary<string, FlowerPatchVisual> flowerPatchVisuals =
        new Dictionary<string, FlowerPatchVisual>(StringComparer.Ordinal);
    private readonly List<SpriteRenderer> treeRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> rockRenderers = new List<SpriteRenderer>();

    private GameObject root;
    private Grid grid;
    private WildlifeHabitatDecorationPaletteSO palette;
    private readonly IResourcesAssetLoader resourcesAssetLoader;

    public WildlifeHabitatDecorationRuntime(IResourcesAssetLoader resourcesAssetLoader = null)
    {
        this.resourcesAssetLoader = resourcesAssetLoader;
    }

    public int FlowerPatchCount => flowerPatchVisuals.Count;
    public int TreeCount => treeRenderers.Count(renderer => renderer != null);
    public int RockCount => rockRenderers.Count(renderer => renderer != null);
    public bool IsReady => root != null && palette != null && palette.IsComplete;

    public void Rebuild(
        Grid runtimeGrid,
        IReadOnlyList<WildlifeHabitatPatch> patches,
        WildlifeHabitatDecorationPaletteSO explicitPalette = null)
    {
        Clear();
        grid = runtimeGrid;
        palette = explicitPalette != null
            ? explicitPalette
            : resourcesAssetLoader?.LoadOptional<WildlifeHabitatDecorationPaletteSO>(
                WildlifeHabitatDecorationPaletteSO.ResourcePath);
        if (grid == null || patches == null || patches.Count == 0 || palette == null || !palette.IsComplete)
        {
            return;
        }

        root = new GameObject(RootName);
        DungeonRuntimeHierarchy.Parent(root, DungeonRuntimeHierarchy.Exterior);

        foreach (WildlifeHabitatPatch patch in patches.Where(patch => patch != null))
        {
            if (patch.HabitatType is WildlifeHabitatType.Grass or WildlifeHabitatType.Brush)
            {
                CreateFlowerPatchVisual(patch);
            }

            if (patch.HabitatType == WildlifeHabitatType.Brush)
            {
                CreatePersistentDecoration(
                    "HabitatTree_" + patch.PatchId,
                    ResolveSurfaceCell(patch.Center),
                    palette.TreeSprites,
                    TreeSortingOrder,
                    treeRenderers,
                    StableHash(patch.PatchId, 31));
            }
            else if (patch.HabitatType is WildlifeHabitatType.Burrow or WildlifeHabitatType.Lair)
            {
                CreatePersistentDecoration(
                    "HabitatRock_" + patch.PatchId,
                    ResolveSurfaceCell(patch.Center),
                    palette.RockSprites,
                    RockSortingOrder,
                    rockRenderers,
                    StableHash(patch.PatchId, 47));
            }
        }

        ScatterPersistentDecorations(patches);
        Refresh(patches);
    }

    public void Refresh(IReadOnlyList<WildlifeHabitatPatch> patches)
    {
        if (patches == null || flowerPatchVisuals.Count == 0)
        {
            return;
        }

        foreach (WildlifeHabitatPatch patch in patches)
        {
            RefreshPatch(patch);
        }
    }

    public void RefreshPatch(WildlifeHabitatPatch patch)
    {
        if (patch == null
            || !flowerPatchVisuals.TryGetValue(patch.PatchId, out FlowerPatchVisual visual))
        {
            return;
        }

        int visibleCount = CalculateVisibleFlowerCount(patch.Resource01, visual.Renderers.Count);
        for (int i = 0; i < visual.Renderers.Count; i++)
        {
            SpriteRenderer renderer = visual.Renderers[i];
            if (renderer != null)
            {
                renderer.gameObject.SetActive(i < visibleCount);
            }
        }
    }

    public int GetVisibleFlowerCount(string patchId)
    {
        if (string.IsNullOrWhiteSpace(patchId)
            || !flowerPatchVisuals.TryGetValue(patchId, out FlowerPatchVisual visual))
        {
            return 0;
        }

        return visual.Renderers.Count(renderer => renderer != null && renderer.gameObject.activeSelf);
    }

    public static int CalculateVisibleFlowerCount(float resource01, int rendererCount)
    {
        if (rendererCount <= 0 || resource01 <= FlowerDepletedThreshold)
        {
            return 0;
        }

        float normalized = Mathf.InverseLerp(FlowerDepletedThreshold, 1f, Mathf.Clamp01(resource01));
        return Mathf.Clamp(Mathf.CeilToInt(normalized * rendererCount), 1, rendererCount);
    }

    public void Dispose()
    {
        Clear();
    }

    public void Clear()
    {
        flowerPatchVisuals.Clear();
        treeRenderers.Clear();
        rockRenderers.Clear();
        if (root != null)
        {
            DestroyObject(root);
        }

        root = null;
        grid = null;
        palette = null;
    }

    private void CreateFlowerPatchVisual(WildlifeHabitatPatch patch)
    {
        Vector2Int surfaceCell = ResolveSurfaceCell(patch.Center);
        int requestedCount = patch.HabitatType == WildlifeHabitatType.Brush
            ? palette.FlowersPerBrushPatch
            : palette.FlowersPerGrassPatch;
        int count = Mathf.Clamp(requestedCount, 1, 8);
        FlowerPatchVisual visual = new FlowerPatchVisual { Patch = patch };
        int seed = StableHash(patch.PatchId, 17);
        for (int i = 0; i < count; i++)
        {
            Sprite sprite = SelectSprite(palette.FlowerSprites, seed + i * 13);
            if (sprite == null)
            {
                continue;
            }

            float spacing = count <= 1 ? 0f : Mathf.Lerp(-0.9f, 0.9f, i / (float)(count - 1));
            float jitter = StableSigned01(seed + i * 29) * 0.08f;
            SpriteRenderer renderer = CreateGroundedRenderer(
                "Flower_" + patch.PatchId + "_" + i,
                surfaceCell,
                sprite,
                FlowerSortingOrder + i,
                spacing + jitter,
                1f);
            if (renderer != null)
            {
                visual.Renderers.Add(renderer);
            }
        }

        if (visual.Renderers.Count > 0)
        {
            flowerPatchVisuals[patch.PatchId] = visual;
        }
    }

    private void ScatterPersistentDecorations(IReadOnlyList<WildlifeHabitatPatch> patches)
    {
        List<Vector2Int> cells = grid.GetCells()
            .Where(IsDecorationCell)
            .Select(cell => cell.Position)
            .OrderBy(position => position.x)
            .ThenBy(position => position.y)
            .ToList();
        if (cells.Count == 0)
        {
            return;
        }

        List<int> treeColumns = treeRenderers
            .Where(renderer => renderer != null)
            .Select(renderer => grid.GetXY(renderer.transform.position).x)
            .ToList();
        List<int> rockColumns = rockRenderers
            .Where(renderer => renderer != null)
            .Select(renderer => grid.GetXY(renderer.transform.position).x)
            .ToList();
        HashSet<int> forageCenters = patches
            .Where(patch => patch != null
                && patch.HabitatType is WildlifeHabitatType.Grass or WildlifeHabitatType.Brush)
            .Select(patch => patch.Center.x)
            .ToHashSet();

        foreach (Vector2Int cell in cells)
        {
            int hash = StableHash(cell.x + ":" + cell.y, 73);
            bool nearForage = forageCenters.Any(x => Mathf.Abs(x - cell.x) <= 2);
            if (!nearForage
                && PositiveModulo(hash, palette.ScatteredTreeSpacing) == 0
                && treeColumns.All(x => Mathf.Abs(x - cell.x) >= 5))
            {
                SpriteRenderer renderer = CreatePersistentDecoration(
                    "ScatteredTree_" + cell.x + "_" + cell.y,
                    cell,
                    palette.TreeSprites,
                    TreeSortingOrder,
                    treeRenderers,
                    hash);
                if (renderer != null)
                {
                    treeColumns.Add(cell.x);
                }

                continue;
            }

            if (!nearForage
                && PositiveModulo(hash / 7, palette.ScatteredRockSpacing) == 0
                && rockColumns.All(x => Mathf.Abs(x - cell.x) >= 2))
            {
                SpriteRenderer renderer = CreatePersistentDecoration(
                    "ScatteredRock_" + cell.x + "_" + cell.y,
                    cell,
                    palette.RockSprites,
                    RockSortingOrder,
                    rockRenderers,
                    hash + 101);
                if (renderer != null)
                {
                    rockColumns.Add(cell.x);
                }
            }
        }
    }

    private SpriteRenderer CreatePersistentDecoration(
        string objectName,
        Vector2Int position,
        IReadOnlyList<Sprite> sprites,
        int sortingOrder,
        List<SpriteRenderer> collection,
        int seed)
    {
        Sprite sprite = SelectSprite(sprites, seed);
        if (sprite == null)
        {
            return null;
        }

        SpriteRenderer renderer = CreateGroundedRenderer(
            objectName,
            position,
            sprite,
            sortingOrder,
            StableSigned01(seed + 53) * 0.14f,
            1f);
        if (renderer != null)
        {
            renderer.flipX = (seed & 1) == 0;
            collection.Add(renderer);
        }

        return renderer;
    }

    private SpriteRenderer CreateGroundedRenderer(
        string objectName,
        Vector2Int position,
        Sprite sprite,
        int sortingOrder,
        float xOffset,
        float scale)
    {
        if (root == null || sprite == null || grid == null)
        {
            return null;
        }

        GameObject entry = new GameObject(objectName);
        entry.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = entry.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = SortingLayerName;
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.white;
        renderer.transform.localScale = Vector3.one * Mathf.Max(0.1f, scale);

        Vector3 floor = grid.GetWorldPos(position);
        float groundOffset = -sprite.bounds.min.y * renderer.transform.localScale.y;
        renderer.transform.position = new Vector3(
            floor.x + xOffset,
            floor.y + groundOffset + 0.015f,
            -0.01f);
        return renderer;
    }

    private Vector2Int ResolveSurfaceCell(Vector2Int requested)
    {
        GridCell exact = grid.GetGridCell(requested);
        if (IsDecorationCell(exact))
        {
            return requested;
        }

        GridCell nearest = grid.GetCells()
            .Where(IsDecorationCell)
            .OrderBy(cell => Mathf.Abs(cell.Position.x - requested.x) + Mathf.Abs(cell.Position.y - requested.y))
            .FirstOrDefault();
        return nearest != null ? nearest.Position : requested;
    }

    private bool IsDecorationCell(GridCell cell)
    {
        return cell != null
            && cell.AreaType == GridCellAreaType.ExteriorPath
            && cell.TerrainType == GridCellTerrainType.Dry
            && grid.IsWalkable(cell.Position)
            && WildlifeRuntime.IsOutdoorSurfaceCell(grid, cell);
    }

    private static Sprite SelectSprite(IReadOnlyList<Sprite> sprites, int seed)
    {
        if (sprites == null || sprites.Count == 0)
        {
            return null;
        }

        return sprites[PositiveModulo(seed, sprites.Count)];
    }

    private static int StableHash(string value, int seed)
    {
        unchecked
        {
            uint hash = 2166136261u ^ (uint)seed;
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }

            return (int)hash;
        }
    }

    private static float StableSigned01(int seed)
    {
        uint value = unchecked((uint)seed * 747796405u + 2891336453u);
        value = ((value >> ((int)(value >> 28) + 4)) ^ value) * 277803737u;
        value = (value >> 22) ^ value;
        return (value / (float)uint.MaxValue) * 2f - 1f;
    }

    private static int PositiveModulo(int value, int modulo)
    {
        int divisor = Mathf.Max(1, modulo);
        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static void DestroyObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
