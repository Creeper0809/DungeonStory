using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class DungeonDebugOverlayRenderer
{
    private const string PreferredSortingLayer = "RoomOverlay";

    private readonly Transform root;
    private readonly TMP_FontAsset font;
    private readonly List<SpriteRenderer> cellPool = new List<SpriteRenderer>();
    private readonly List<LineRenderer> linePool = new List<LineRenderer>();
    private readonly List<TextMeshPro> labelPool = new List<TextMeshPro>();
    private readonly Material lineMaterial;
    private readonly Sprite whiteSprite;
    private int activeCells;
    private int activeLines;
    private int activeLabels;

    public DungeonDebugOverlayRenderer(
        Transform parent,
        TMP_FontAsset font,
        string rootName = "DungeonDebugWorldOverlay")
    {
        this.font = font;
        GameObject rootObject = new GameObject(
            string.IsNullOrWhiteSpace(rootName)
                ? "DungeonDebugWorldOverlay"
                : rootName);
        rootObject.transform.SetParent(parent, false);
        root = rootObject.transform;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "DungeonDebugOverlayPixel",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);
        whiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        whiteSprite.name = "DungeonDebugOverlaySprite";
        whiteSprite.hideFlags = HideFlags.HideAndDontSave;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            lineMaterial = new Material(shader)
            {
                name = "DungeonDebugOverlayLineMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }

    public Transform Root => root;
    public int ActiveCellCount => activeCells;
    public int ActiveLineCount => activeLines;
    public int ActiveLabelCount => activeLabels;

    public void BeginFrame()
    {
        activeCells = 0;
        activeLines = 0;
        activeLabels = 0;
    }

    public void DrawCell(Grid grid, Vector2Int position, Color color, float inset = 0.08f)
    {
        if (grid == null || !grid.IsValidGridPos(position))
        {
            return;
        }

        SpriteRenderer renderer = GetCell();
        float width = Mathf.Max(0.05f, 1f - inset * 2f);
        float height = Mathf.Max(0.05f, grid.CellWorldHeight - inset * 2f);
        renderer.transform.position = grid.GetWorldPos(position)
            + new Vector3(0f, grid.CellWorldHeight * 0.5f, 0f);
        renderer.transform.localScale = new Vector3(width, height, 1f);
        renderer.color = color;
    }

    public void DrawOutline(Grid grid, Vector2Int position, Color color, float width = 0.045f)
    {
        if (grid == null || !grid.IsValidGridPos(position))
        {
            return;
        }

        Vector3 bottomCenter = grid.GetWorldPos(position);
        float halfWidth = 0.5f;
        float height = grid.CellWorldHeight;
        Vector3 bottomLeft = bottomCenter + Vector3.left * halfWidth;
        Vector3 bottomRight = bottomCenter + Vector3.right * halfWidth;
        Vector3 topLeft = bottomLeft + Vector3.up * height;
        Vector3 topRight = bottomRight + Vector3.up * height;
        DrawLine(bottomLeft, bottomRight, color, width);
        DrawLine(bottomRight, topRight, color, width);
        DrawLine(topRight, topLeft, color, width);
        DrawLine(topLeft, bottomLeft, color, width);
    }

    public void DrawLine(Vector3 from, Vector3 to, Color color, float width = 0.05f)
    {
        LineRenderer renderer = GetLine();
        renderer.startColor = color;
        renderer.endColor = color;
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.positionCount = 2;
        renderer.SetPosition(0, from);
        renderer.SetPosition(1, to);
    }

    public void DrawLabel(Vector3 worldPosition, string text, Color color, float size = 2.4f)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        TextMeshPro label = GetLabel();
        label.transform.position = worldPosition;
        label.text = text;
        label.color = color;
        label.fontSize = size;
    }

    public void EndFrame()
    {
        DisableUnused(cellPool, activeCells);
        DisableUnused(linePool, activeLines);
        DisableUnused(labelPool, activeLabels);
    }

    public void Clear()
    {
        activeCells = 0;
        activeLines = 0;
        activeLabels = 0;
        EndFrame();
    }

    public void Dispose()
    {
        if (root != null)
        {
            Object.Destroy(root.gameObject);
        }

        if (lineMaterial != null)
        {
            Object.Destroy(lineMaterial);
        }

        if (whiteSprite != null)
        {
            Texture2D texture = whiteSprite.texture;
            Object.Destroy(whiteSprite);
            if (texture != null)
            {
                Object.Destroy(texture);
            }
        }
    }

    private SpriteRenderer GetCell()
    {
        SpriteRenderer renderer;
        if (activeCells < cellPool.Count)
        {
            renderer = cellPool[activeCells];
        }
        else
        {
            GameObject item = new GameObject($"Cell_{cellPool.Count}", typeof(SpriteRenderer));
            item.transform.SetParent(root, false);
            renderer = item.GetComponent<SpriteRenderer>();
            renderer.sprite = whiteSprite;
            renderer.sortingLayerName = ResolveSortingLayer();
            renderer.sortingOrder = 5;
            cellPool.Add(renderer);
        }

        activeCells++;
        renderer.gameObject.SetActive(true);
        return renderer;
    }

    private LineRenderer GetLine()
    {
        LineRenderer renderer;
        if (activeLines < linePool.Count)
        {
            renderer = linePool[activeLines];
        }
        else
        {
            GameObject item = new GameObject($"Line_{linePool.Count}", typeof(LineRenderer));
            item.transform.SetParent(root, false);
            renderer = item.GetComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.numCapVertices = 0;
            renderer.numCornerVertices = 0;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.alignment = LineAlignment.View;
            renderer.sortingLayerName = ResolveSortingLayer();
            renderer.sortingOrder = 6;
            if (lineMaterial != null)
            {
                renderer.sharedMaterial = lineMaterial;
            }
            linePool.Add(renderer);
        }

        activeLines++;
        renderer.gameObject.SetActive(true);
        return renderer;
    }

    private TextMeshPro GetLabel()
    {
        TextMeshPro label;
        if (activeLabels < labelPool.Count)
        {
            label = labelPool[activeLabels];
        }
        else
        {
            GameObject item = new GameObject($"Label_{labelPool.Count}", typeof(TextMeshPro));
            item.transform.SetParent(root, false);
            label = item.GetComponent<TextMeshPro>();
            label.font = font;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.sortingLayerID = SortingLayer.NameToID(ResolveSortingLayer());
            label.sortingOrder = 7;
            labelPool.Add(label);
        }

        activeLabels++;
        label.gameObject.SetActive(true);
        return label;
    }

    private static void DisableUnused<T>(IReadOnlyList<T> pool, int active)
        where T : Component
    {
        for (int index = active; index < pool.Count; index++)
        {
            if (pool[index] != null)
            {
                pool[index].gameObject.SetActive(false);
            }
        }
    }

    private static string ResolveSortingLayer()
    {
        int preferredId = SortingLayer.NameToID(PreferredSortingLayer);
        return preferredId != 0 ? PreferredSortingLayer : "Default";
    }
}
