using System.Collections.Generic;
using UnityEngine;

public class GridGhostObject : MonoBehaviour
{
    private const float BlockedPreviewTintStrength = 0.45f;
    private const float PreviewAlpha = 1f;
    private const string MinimumPreviewSortingLayerName = "Default";
    private const int MinimumPreviewSortingOrder = 200;

    [SerializeField] private GameObject ghostObject;
    [SerializeField] private Color blockedColor = Color.red;
    [SerializeField] private string previewSortingLayerName = MinimumPreviewSortingLayerName;
    [SerializeField] private int previewSortingOrder = 200;

    private SpriteRenderer ghostSpriteRenderer;
    private readonly List<SpriteRenderer> repeatedSpriteRenderers = new List<SpriteRenderer>();
    private Vector2 visualSize = Vector2.zero;
    private Vector3 anchorOffset = Vector3.zero;

    public bool IsHidden { get; private set; } = true;

    private void Awake()
    {
        Initialize();
    }

    private void OnValidate()
    {
        Initialize();
    }

    public void Initialize(GameObject target = null)
    {
        if (target != null)
        {
            ghostObject = target;
        }

        if (ghostObject == null && transform.childCount > 0)
        {
            ghostObject = transform.GetChild(0).gameObject;
        }

        ghostSpriteRenderer = ghostObject != null
            ? ghostObject.GetComponent<SpriteRenderer>()
            : null;

        ConfigurePreviewRenderer(ghostSpriteRenderer);

        if (ghostSpriteRenderer != null && repeatedSpriteRenderers.Count == 0)
        {
            repeatedSpriteRenderers.Add(ghostSpriteRenderer);
        }

        ConfigurePreviewRenderers();
    }

    public void Show(Sprite sprite)
    {
        EnsureInitialized();
        IsHidden = false;
        HideRepeatedRenderers(1);
        if (ghostSpriteRenderer != null)
        {
            ghostSpriteRenderer.gameObject.SetActive(true);
            ghostSpriteRenderer.sprite = sprite;
            ghostSpriteRenderer.color = GetPreviewColor(true);
            ApplyFootprintSize();
        }
    }

    public void ShowRepeated(
        Sprite sprite,
        IReadOnlyList<Vector3> worldPositions,
        Vector2 tileVisualSize,
        IReadOnlyList<bool> buildableStates = null)
    {
        EnsureInitialized();
        int count = worldPositions != null ? worldPositions.Count : 0;
        if (count == 0)
        {
            Hide();
            return;
        }

        IsHidden = false;
        EnsureRepeatedRendererCount(count);

        for (int i = 0; i < repeatedSpriteRenderers.Count; i++)
        {
            SpriteRenderer renderer = repeatedSpriteRenderers[i];
            if (renderer == null) continue;

            bool active = i < count;
            renderer.gameObject.SetActive(active);
            if (!active)
            {
                renderer.sprite = null;
                continue;
            }

            renderer.sprite = sprite;
            bool buildable = buildableStates == null
                || i >= buildableStates.Count
                || buildableStates[i];
            renderer.color = GetPreviewColor(buildable);
            Vector3 tileAnchorOffset = ApplyVisualSize(renderer, tileVisualSize);
            renderer.transform.position = worldPositions[i] + tileAnchorOffset;
        }
    }

    public void Hide()
    {
        EnsureInitialized();
        IsHidden = true;
        foreach (SpriteRenderer renderer in repeatedSpriteRenderers)
        {
            if (renderer == null) continue;

            renderer.sprite = null;
            renderer.gameObject.SetActive(false);
        }
    }

    public void SetBuildable(bool buildable)
    {
        EnsureInitialized();
        if (ghostSpriteRenderer != null)
        {
            ghostSpriteRenderer.color = GetPreviewColor(buildable);
        }
    }

    public void SetWorldPosition(Vector3 worldPosition)
    {
        EnsureInitialized();
        if (ghostObject == null) return;

        ghostObject.transform.position = worldPosition + anchorOffset;
    }

    public void SetSize(Vector2 size)
    {
        EnsureInitialized();
        visualSize = size;
        ApplyFootprintSize();
    }

    private void ApplyFootprintSize()
    {
        anchorOffset = ApplyVisualSize(ghostSpriteRenderer, visualSize);
    }

    private Vector3 ApplyVisualSize(SpriteRenderer renderer, Vector2 size)
    {
        if (renderer == null
            || renderer.sprite == null
            || size.x <= 0f
            || size.y <= 0f)
        {
            return Vector3.zero;
        }

        Sprite sprite = renderer.sprite;
        Vector2 spriteSize = sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            return Vector3.zero;
        }

        Vector3 scale = new Vector3(
            size.x / spriteSize.x,
            size.y / spriteSize.y,
            1f);
        Vector3 desiredCenter = new Vector3(0f, size.y * 0.5f, 0f);
        Vector3 scaledBoundsCenter = new Vector3(
            sprite.bounds.center.x * scale.x,
            sprite.bounds.center.y * scale.y,
            0f);

        renderer.transform.localScale = scale;
        return desiredCenter - scaledBoundsCenter;
    }

    private Color GetPreviewColor(bool buildable)
    {
        Color tint = buildable
            ? Color.white
            : Color.Lerp(Color.white, blockedColor, BlockedPreviewTintStrength);
        return WithPreviewAlpha(tint);
    }

    private Color WithPreviewAlpha(Color color)
    {
        color.a = PreviewAlpha;
        return color;
    }

    private void EnsureRepeatedRendererCount(int count)
    {
        if (ghostSpriteRenderer == null) return;

        if (repeatedSpriteRenderers.Count == 0)
        {
            repeatedSpriteRenderers.Add(ghostSpriteRenderer);
        }

        while (repeatedSpriteRenderers.Count < count)
        {
            GameObject repeatedObject = new GameObject($"GhostTile{repeatedSpriteRenderers.Count}");
            repeatedObject.transform.SetParent(transform, false);

            SpriteRenderer renderer = repeatedObject.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = ghostSpriteRenderer.sharedMaterial;
            ConfigurePreviewRenderer(renderer);
            repeatedSpriteRenderers.Add(renderer);
        }
    }

    private void ConfigurePreviewRenderers()
    {
        foreach (SpriteRenderer renderer in repeatedSpriteRenderers)
        {
            ConfigurePreviewRenderer(renderer);
        }
    }

    private void ConfigurePreviewRenderer(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.drawMode = SpriteDrawMode.Simple;
        string targetSortingLayer = ResolvePreviewSortingLayerName();
        renderer.sortingLayerName = targetSortingLayer;
        renderer.sortingOrder = Mathf.Max(previewSortingOrder, MinimumPreviewSortingOrder);
    }

    private string ResolvePreviewSortingLayerName()
    {
        if (string.IsNullOrWhiteSpace(previewSortingLayerName))
        {
            return MinimumPreviewSortingLayerName;
        }

        int requestedLayerValue = SortingLayer.GetLayerValueFromName(previewSortingLayerName);
        int minimumLayerValue = SortingLayer.GetLayerValueFromName(MinimumPreviewSortingLayerName);
        return requestedLayerValue >= minimumLayerValue
            ? previewSortingLayerName
            : MinimumPreviewSortingLayerName;
    }

    private void HideRepeatedRenderers(int keepActiveCount)
    {
        for (int i = keepActiveCount; i < repeatedSpriteRenderers.Count; i++)
        {
            SpriteRenderer renderer = repeatedSpriteRenderers[i];
            if (renderer == null) continue;

            renderer.sprite = null;
            renderer.gameObject.SetActive(false);
        }
    }

    private void EnsureInitialized()
    {
        if (ghostObject != null && ghostSpriteRenderer != null) return;

        Initialize();
    }
}
