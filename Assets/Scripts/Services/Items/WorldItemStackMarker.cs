using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

public sealed class WorldItemStackMarker : MonoBehaviour, IGridOccupant
{
    private Grid grid;
    private IWorldItemMarkerDataSource dataSource;
    private IMainCameraProvider mainCameraProvider;
    private TMP_FontAsset markerFont;
    private Sprite fallbackSprite;
    private Vector2Int position;
    private SpriteRenderer spriteRenderer;
    private TextMeshPro quantityText;
    private TextMeshPro kindText;
    private GameObject tooltipRoot;
    private SpriteRenderer tooltipBackground;
    private TextMeshPro tooltipText;
    private bool tooltipVisible;

    public int GridId => -500000 - Mathf.Abs(position.GetHashCode());
    public bool IsGridDestroyed => this == null || gameObject == null;
    public bool IsGridVisitable => false;
    public bool IsGridMovement => false;
    public Vector2Int Position => position;

    public static WorldItemStackMarker Create(
        IWorldItemMarkerDataSource dataSource,
        IMainCameraProvider mainCameraProvider,
        TMP_FontAsset markerFont,
        Grid grid,
        Vector2Int position)
    {
        GameObject markerObject = new GameObject($"ItemPile_{position.x}_{position.y}");
        DungeonRuntimeHierarchy.Parent(markerObject, DungeonRuntimeHierarchy.Items);
        WorldItemStackMarker marker = markerObject.AddComponent<WorldItemStackMarker>();
        marker.Initialize(dataSource, mainCameraProvider, markerFont, grid, position);
        return marker;
    }

    private void Initialize(
        IWorldItemMarkerDataSource source,
        IMainCameraProvider cameraProvider,
        TMP_FontAsset font,
        Grid sourceGrid,
        Vector2Int gridPosition)
    {
        dataSource = source;
        mainCameraProvider = cameraProvider;
        markerFont = font;
        grid = sourceGrid;
        position = gridPosition;
        transform.position = grid.GetWorldPos(position) + new Vector3(0f, 0.18f, 0f);

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetFallbackSprite();
        spriteRenderer.color = new Color(0.9f, 0.82f, 0.36f, 0.92f);
        spriteRenderer.sortingLayerName = "DungeonBackObject";
        spriteRenderer.sortingOrder = 640;

        BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(0.62f, 0.54f);

        quantityText = CreateWorldText("Quantity", new Vector3(0f, 0.22f, 0f), 2.2f);
        kindText = CreateWorldText("KindCount", new Vector3(0f, -0.18f, 0f), 1.55f);
        EnsureTooltip();
        SetTooltipVisible(false);

        grid.RegisterOccupant(this, GridLayer.Item, new[] { position }, connectPositions: false);
    }

    private void Update()
    {
        if (dataSource == null || grid == null)
        {
            SetTooltipVisible(false);
            return;
        }

        Camera camera = mainCameraProvider != null ? mainCameraProvider.Camera : null;
        if (camera == null || !TryGetPointerPosition(out Vector3 screenPosition))
        {
            SetTooltipVisible(false);
            return;
        }

        screenPosition.z = -camera.transform.position.z;
        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        WorldItemPileSnapshot pile = null;
        bool shouldShow = grid.GetXY(worldPosition) == position
            && dataSource.TryGetPileAt(position, out pile)
            && pile.Representative != null;

        SetTooltipVisible(shouldShow);
        if (shouldShow)
        {
            RefreshTooltip(pile);
        }
    }

    public void Refresh(WorldItemPileSnapshot pile)
    {
        if (pile == null || pile.Representative == null)
        {
            return;
        }

        if (pile.Representative.Sprite != null)
        {
            spriteRenderer.sprite = pile.Representative.Sprite;
            spriteRenderer.color = Color.white;
        }
        else
        {
            spriteRenderer.sprite = GetFallbackSprite();
            spriteRenderer.color = pile.HasReservedItems
                ? new Color(0.82f, 0.72f, 0.32f, 0.82f)
                : new Color(0.9f, 0.82f, 0.36f, 0.92f);
        }

        quantityText.text = pile.TotalQuantity.ToString(CultureInfo.InvariantCulture);
        kindText.text = pile.KindCount > 1
            ? pile.KindCount.ToString(CultureInfo.InvariantCulture) + "종"
            : string.Empty;
    }

    private void EnsureTooltip()
    {
        if (tooltipRoot != null)
        {
            return;
        }

        tooltipRoot = new GameObject("ItemPileTooltip");
        tooltipRoot.transform.SetParent(transform, false);
        tooltipRoot.transform.localPosition = new Vector3(0f, 0.76f, 0f);

        tooltipBackground = tooltipRoot.AddComponent<SpriteRenderer>();
        tooltipBackground.sprite = GetFallbackSprite();
        tooltipBackground.color = new Color(0.03f, 0.06f, 0.07f, 0.92f);
        tooltipBackground.sortingLayerName = "DungeonBackObject";
        tooltipBackground.sortingOrder = 670;
        tooltipBackground.transform.localScale = new Vector3(4.5f, 0.72f, 1f);

        tooltipText = CreateWorldText("TooltipText", Vector3.zero, 0.92f);
        tooltipText.transform.SetParent(tooltipRoot.transform, false);
        tooltipText.transform.localPosition = new Vector3(0f, -0.02f, 0f);
        tooltipText.alignment = TextAlignmentOptions.Center;
        tooltipText.fontStyle = FontStyles.Bold;
        tooltipText.sortingLayerID = tooltipBackground.sortingLayerID;
        tooltipText.sortingOrder = tooltipBackground.sortingOrder + 1;
    }

    private void RefreshTooltip(WorldItemPileSnapshot pile)
    {
        EnsureTooltip();
        string text = BuildTooltipText(pile);
        tooltipText.text = text;
        Vector2 preferred = tooltipText.GetPreferredValues(text);
        float width = Mathf.Clamp(preferred.x + 0.7f, 2.4f, 7.4f);
        tooltipBackground.transform.localScale = new Vector3(width, 0.72f, 1f);
    }

    private void SetTooltipVisible(bool visible)
    {
        if (tooltipVisible == visible)
        {
            return;
        }

        tooltipVisible = visible;
        if (tooltipRoot != null)
        {
            tooltipRoot.SetActive(visible);
        }
    }

    private static string BuildTooltipText(WorldItemPileSnapshot pile)
    {
        if (pile == null || pile.Representative == null)
        {
            return string.Empty;
        }

        WorldItemStackSnapshot representative = pile.Representative;
        int otherKinds = Mathf.Max(0, pile.KindCount - 1);
        string label = $"{representative.DisplayName} x{representative.Quantity}";
        if (otherKinds > 0)
        {
            label += $" 외 {otherKinds}종";
        }

        label += $" · {pile.TotalWeight:0.#}kg";
        if (pile.HasReservedItems)
        {
            label += " · 일부 예약됨";
        }

        return label;
    }

    private static bool TryGetPointerPosition(out Vector3 screenPosition)
    {
        if (DungeonAutomationInputState.TryGetPointerPosition(out screenPosition))
        {
            return true;
        }

        if (Mouse.current != null)
        {
            Vector2 inputSystemPosition = Mouse.current.position.ReadValue();
            screenPosition = new Vector3(inputSystemPosition.x, inputSystemPosition.y, 0f);
            return !float.IsNaN(screenPosition.x)
                && !float.IsNaN(screenPosition.y)
                && !float.IsInfinity(screenPosition.x)
                && !float.IsInfinity(screenPosition.y);
        }

        screenPosition = Input.mousePosition;
        return true;
    }

    private Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        fallbackSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            4f);
        return fallbackSprite;
    }

    private TextMeshPro CreateWorldText(string objectName, Vector3 localPosition, float fontSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(TextMeshPro));
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = localPosition;
        TextMeshPro text = textObject.GetComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.color = Color.white;
        if (markerFont != null)
        {
            text.font = markerFont;
        }
        text.sortingLayerID = spriteRenderer.sortingLayerID;
        text.sortingOrder = spriteRenderer.sortingOrder + 1;
        return text;
    }

    private void OnDestroy()
    {
        if (grid != null)
        {
            grid.RemoveOccupant(this, GridLayer.Item, new[] { position }, disconnectPositions: false);
        }

        if (fallbackSprite != null)
        {
            if (Application.isPlaying)
            {
                Destroy(fallbackSprite);
            }
            else
            {
                DestroyImmediate(fallbackSprite);
            }
        }
    }
}
