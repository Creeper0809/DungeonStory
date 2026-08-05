using System;
using UnityEngine;

public sealed class WildlifeVisualPresentation
{
    private const string VisualRootName = "WildlifeVisual";
    private const string HealthRootName = "WildlifeHealth";
    private const string DefaultSortingLayerName = "Default";
    private const int DefaultSortingOrder = 120;
    private const int MarkerSortingOrderOffset = 36;
    private const int HealthSortingOrderOffset = 32;
    private const float HealthBarWidth = 0.72f;
    private const float HealthBarHeight = 0.045f;
    private const float MovementBobHeight = 0.035f;

    private readonly WildlifeActor owner;
    private Transform visualRoot;
    private SpriteRenderer visualRenderer;
    private SpriteRenderer markerRenderer;
    private Transform healthRoot;
    private LineRenderer healthBackgroundLine;
    private LineRenderer healthFillLine;
    private Vector3 visualRootRestLocalPosition;
    private string currentSortingLayerName = DefaultSortingLayerName;
    private int currentSortingOrder = DefaultSortingOrder;

    public WildlifeVisualPresentation(WildlifeActor owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public SpriteRenderer VisualRenderer => visualRenderer;
    public bool IsHealthBarVisibleForDebug =>
        healthRoot != null && healthRoot.gameObject.activeSelf;

    private Transform transform => owner.transform;
    private GameObject gameObject => owner.gameObject;
    private Sprite Sprite => owner.Sprite;
    private string SpeciesId => owner.SpeciesId;
    private string DisplayName => owner.DisplayName;
    private string WildlifeId => owner.WildlifeId;
    private int CurrentHealth => owner.CurrentHealth;
    private int MaxHealth => owner.MaxHealth;
    private WildlifeState State => owner.State;
    private bool IsAlive => owner.IsAlive;
    private bool HuntDesignated => owner.HuntDesignated;
    private bool PriorityHunt => owner.PriorityHunt;
    private bool IsDangerous => owner.IsDangerous;
    private Vector2Int gridPosition => owner.GridPosition;

    private T GetComponent<T>() where T : Component => owner.GetComponent<T>();

    public void ChangeLayer(string layer)
    {
        currentSortingLayerName = string.IsNullOrWhiteSpace(layer)
            ? DefaultSortingLayerName
            : layer;
        ApplyVisualSorting();
    }

    public void SetHorizontalDirection(int horizontalDirection)
    {
        if (visualRenderer != null)
        {
            visualRenderer.flipX = horizontalDirection < 0;
        }
    }

    public void ApplyMovementBob(float normalizedProgress)
    {
        if (visualRoot == null)
        {
            return;
        }

        float bob = Mathf.Sin(Mathf.Clamp01(normalizedProgress) * Mathf.PI)
            * MovementBobHeight;
        visualRoot.localPosition =
            visualRootRestLocalPosition + Vector3.up * bob;
    }

    public void RestorePose()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualRootRestLocalPosition;
        }
    }

    public void EnsureVisual()
    {
        visualRoot = EnsureVisualRoot();
        visualRenderer = visualRoot.GetComponent<SpriteRenderer>();
        if (visualRenderer == null)
        {
            visualRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
        }

        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        if (rootRenderer != null && rootRenderer != visualRenderer)
        {
            if (visualRenderer.sprite == null)
            {
                CopySpriteRenderer(rootRenderer, visualRenderer);
            }

            RemoveRootSpriteRenderer(rootRenderer);
        }

        visualRenderer.sprite = Sprite != null
            ? Sprite
            : WildlifeVisualAssetCache.GetFallbackSprite();
        visualRenderer.color = ResolveFallbackColor();
        ApplyVisualFootAnchor();
        ApplyVisualSorting();
        EnsureMarker();
        EnsureHealthBar();

        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider2D>();
        }

        ConfigureCollider(collider);
        gameObject.name = "Wildlife_" + DisplayName + "_" + WildlifeId;
        UpdateAttachedVisualPositions();
        UpdateMarker();
        UpdateHealthBar(force: true);
    }

    private Transform EnsureVisualRoot()
    {
        Transform root = transform.Find(VisualRootName);
        if (root != null)
        {
            return root;
        }

        GameObject rootObject = new GameObject(VisualRootName);
        root = rootObject.transform;
        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;
        return root;
    }

    private static void CopySpriteRenderer(SpriteRenderer source, SpriteRenderer target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.sprite = source.sprite;
        target.color = source.color;
        target.sharedMaterials = source.sharedMaterials;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        target.maskInteraction = source.maskInteraction;
        target.drawMode = SpriteDrawMode.Simple;
        target.size = source.sprite != null ? (Vector2)source.sprite.bounds.size : Vector2.one;
    }

    private static void RemoveRootSpriteRenderer(SpriteRenderer rootRenderer)
    {
        if (rootRenderer == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(rootRenderer);
            return;
        }

        UnityEngine.Object.DestroyImmediate(rootRenderer);
    }

    private void ApplyVisualFootAnchor()
    {
        if (visualRoot == null || visualRenderer == null || visualRenderer.sprite == null)
        {
            return;
        }

        Bounds bounds = visualRenderer.sprite.bounds;
        visualRootRestLocalPosition = new Vector3(
            -bounds.center.x,
            -bounds.min.y,
            0f);
        visualRoot.localPosition = visualRootRestLocalPosition;
    }

    private void ApplyVisualSorting()
    {
        if (visualRenderer != null)
        {
            visualRenderer.sortingLayerName = currentSortingLayerName;
            visualRenderer.sortingOrder = currentSortingOrder;
        }

        if (markerRenderer != null)
        {
            markerRenderer.sortingLayerName = currentSortingLayerName;
            markerRenderer.sortingOrder = currentSortingOrder + MarkerSortingOrderOffset;
        }

        ApplyLineSorting(healthBackgroundLine, currentSortingLayerName, currentSortingOrder + HealthSortingOrderOffset);
        ApplyLineSorting(healthFillLine, currentSortingLayerName, currentSortingOrder + HealthSortingOrderOffset + 1);
    }

    public void RefreshSortingForGridPosition()
    {
        currentSortingOrder = DefaultSortingOrder + Mathf.Clamp(gridPosition.y, 0, 20) * 2;
        ApplyVisualSorting();
    }

    private void ConfigureCollider(BoxCollider2D collider)
    {
        Bounds bounds = visualRenderer != null && visualRenderer.sprite != null
            ? visualRenderer.sprite.bounds
            : new Bounds(Vector3.zero, new Vector3(1f, 1f, 0f));
        float width = Mathf.Clamp(bounds.size.x * 0.72f, 0.45f, 1.25f);
        float height = Mathf.Clamp(bounds.size.y * 0.72f, 0.35f, 1.05f);
        collider.size = new Vector2(width, height);
        collider.offset = new Vector2(0f, height * 0.5f);
    }

    private Color ResolveFallbackColor()
    {
        if (Sprite != null)
        {
            return Color.white;
        }

        return IsDangerous
            ? new Color(0.55f, 0.18f, 0.2f, 1f)
            : new Color(0.55f, 0.72f, 0.48f, 1f);
    }

    private void EnsureMarker()
    {
        if (markerRenderer != null)
        {
            return;
        }

        GameObject marker = new GameObject("WildlifeStateMarker");
        marker.transform.SetParent(transform, false);
        marker.transform.localScale = new Vector3(0.48f, 0.48f, 1f);
        markerRenderer = marker.AddComponent<SpriteRenderer>();
        markerRenderer.sprite = WildlifeVisualAssetCache.GetMarkerSprite();
        markerRenderer.enabled = false;
        ApplyVisualSorting();
    }

    private void EnsureHealthBar()
    {
        if (healthRoot == null)
        {
            Transform existingRoot = transform.Find(HealthRootName);
            healthRoot = existingRoot != null
                ? existingRoot
                : new GameObject(HealthRootName).transform;
            healthRoot.SetParent(transform, false);
            healthRoot.localRotation = Quaternion.identity;
            healthRoot.localScale = Vector3.one;
        }

        healthBackgroundLine = EnsureHealthLine("HealthBackground", new Color(0.02f, 0.04f, 0.05f, 0.82f));
        healthFillLine = EnsureHealthLine("HealthFill", new Color(0.32f, 0.84f, 0.58f, 1f));
        ApplyVisualSorting();
    }

    private LineRenderer EnsureHealthLine(string objectName, Color color)
    {
        Transform child = healthRoot.Find(objectName);
        if (child == null)
        {
            child = new GameObject(objectName).transform;
            child.SetParent(healthRoot, false);
        }

        if (!child.TryGetComponent(out LineRenderer line))
        {
            line = child.gameObject.AddComponent<LineRenderer>();
        }

        line.useWorldSpace = false;
        line.positionCount = 2;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.widthMultiplier = HealthBarHeight;
        Material material = WildlifeVisualAssetCache.ResolveLineMaterial();
        if (material != null)
        {
            line.sharedMaterial = material;
        }

        line.startColor = color;
        line.endColor = color;
        return line;
    }

    private void UpdateAttachedVisualPositions()
    {
        float top = GetVisualTopLocalY();
        if (markerRenderer != null)
        {
            markerRenderer.transform.localPosition = new Vector3(0f, top + 0.18f, -0.01f);
        }

        if (healthRoot != null)
        {
            healthRoot.localPosition = new Vector3(0f, top + 0.06f, -0.01f);
        }
    }

    private float GetVisualTopLocalY()
    {
        if (visualRoot == null || visualRenderer == null || visualRenderer.sprite == null)
        {
            return 0.9f;
        }

        return visualRoot.localPosition.y + visualRenderer.sprite.bounds.max.y;
    }

    public void UpdateMarker()
    {
        if (markerRenderer == null)
        {
            return;
        }

        Color markerColor;
        bool visible = true;
        if (!IsAlive)
        {
            visible = false;
            markerColor = Color.clear;
        }
        else if (HuntDesignated)
        {
            markerColor = PriorityHunt
                ? new Color(1f, 0.12f, 0.08f, 1f)
                : new Color(0.82f, 0.16f, 0.16f, 1f);
        }
        else if (State == WildlifeState.Fleeing || State == WildlifeState.Leaving)
        {
            markerColor = new Color(1f, 0.83f, 0.18f, 1f);
        }
        else if (IsDangerous || State == WildlifeState.PredatorStalking || State == WildlifeState.Retaliating)
        {
            markerColor = new Color(0.72f, 0.05f, 0.09f, 1f);
        }
        else
        {
            visible = false;
            markerColor = Color.clear;
        }

        markerRenderer.enabled = visible;
        markerRenderer.color = markerColor;
    }

    public void UpdateHealthBar(bool force = false)
    {
        if (healthRoot == null || healthBackgroundLine == null || healthFillLine == null)
        {
            return;
        }

        bool visible = IsAlive && CurrentHealth < MaxHealth;
        if (healthRoot.gameObject.activeSelf != visible)
        {
            healthRoot.gameObject.SetActive(visible);
        }

        if (!visible && !force)
        {
            return;
        }

        float health01 = Mathf.Clamp01(CurrentHealth / Mathf.Max(1f, MaxHealth));
        SetLineSpan(healthBackgroundLine, -HealthBarWidth * 0.5f, HealthBarWidth * 0.5f);
        float fillRight = Mathf.Lerp(-HealthBarWidth * 0.5f, HealthBarWidth * 0.5f, health01);
        SetLineSpan(healthFillLine, -HealthBarWidth * 0.5f, fillRight);

        Color color = health01 > 0.6f
            ? new Color(0.32f, 0.84f, 0.58f, 1f)
            : health01 > 0.3f
                ? new Color(0.95f, 0.74f, 0.22f, 1f)
                : new Color(0.93f, 0.22f, 0.18f, 1f);
        healthFillLine.startColor = color;
        healthFillLine.endColor = color;
    }

    private static void SetLineSpan(LineRenderer line, float left, float right)
    {
        if (line == null)
        {
            return;
        }

        line.SetPosition(0, new Vector3(left, 0f, 0f));
        line.SetPosition(1, new Vector3(right, 0f, 0f));
    }

    private static void ApplyLineSorting(LineRenderer line, string layerName, int order)
    {
        if (line == null)
        {
            return;
        }

        line.sortingLayerName = string.IsNullOrWhiteSpace(layerName)
            ? DefaultSortingLayerName
            : layerName;
        line.sortingOrder = order;
    }

}
