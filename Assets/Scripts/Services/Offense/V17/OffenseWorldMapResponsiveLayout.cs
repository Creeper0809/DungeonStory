using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class OffenseWorldMapResponsiveLayout : MonoBehaviour
{
    private const float PortraitAspectThreshold = 0.86f;

    private RectTransform header;
    private RectTransform mapViewport;
    private RectTransform mapContent;
    private RectTransform actionViewport;
    private RectTransform detail;
    private CanvasScaler canvasScaler;
    private bool isBound;
    private bool appliedPortrait;
    private Vector2 lastSize;

    public bool IsPortrait => appliedPortrait;

    public void Bind(
        RectTransform header,
        RectTransform mapViewport,
        RectTransform mapContent,
        RectTransform actionViewport,
        RectTransform detail)
    {
        this.header = header;
        this.mapViewport = mapViewport;
        this.mapContent = mapContent;
        this.actionViewport = actionViewport;
        this.detail = detail;
        canvasScaler = GetComponentInParent<CanvasScaler>();
        isBound = header != null
            && mapViewport != null
            && mapContent != null
            && actionViewport != null
            && detail != null;
        ApplyLayout(force: true);
    }

    private void OnEnable()
    {
        ApplyLayout(force: true);
    }

    private void OnRectTransformDimensionsChange()
    {
        ApplyLayout(force: false);
    }

    private void ApplyLayout(bool force)
    {
        if (!isBound)
        {
            return;
        }

        RectTransform panel = transform as RectTransform;
        if (panel == null)
        {
            return;
        }

        Vector2 size = panel.rect.size;
        if (size.x <= 1f || size.y <= 1f)
        {
            return;
        }

        bool portrait = Screen.height > Screen.width
            || size.x / size.y < PortraitAspectThreshold;
        if (!force
            && portrait == appliedPortrait
            && Vector2.SqrMagnitude(size - lastSize) < 1f)
        {
            return;
        }

        appliedPortrait = portrait;
        lastSize = size;
        if (portrait)
        {
            ApplyPortrait();
        }
        else
        {
            ApplyLandscape();
        }

        mapViewport.GetComponent<OffenseV17MapInput>()?.ResetView();
    }

    private void ApplyLandscape()
    {
        ConfigureCanvas(new Vector2(1600f, 900f));
        mapContent.sizeDelta = new Vector2(1040f, 720f);
        SetRect(
            header,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(24f, -66f),
            new Vector2(-24f, -18f));
        SetRect(
            mapViewport,
            new Vector2(0f, 0f),
            new Vector2(0.72f, 0.9f),
            new Vector2(20f, 16f),
            new Vector2(-8f, -10f));
        SetRect(
            actionViewport,
            new Vector2(0.73f, 0.34f),
            new Vector2(1f, 0.9f),
            new Vector2(12f, 12f),
            new Vector2(-20f, -10f));
        SetRect(
            detail,
            new Vector2(0.73f, 0f),
            new Vector2(1f, 0.32f),
            new Vector2(12f, 14f),
            new Vector2(-20f, -8f));
        ConfigureDetailText(20f, 14f);
    }

    private void ApplyPortrait()
    {
        ConfigureCanvas(new Vector2(900f, 1600f));
        mapContent.sizeDelta = new Vector2(820f, 760f);
        SetRect(
            header,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(18f, -62f),
            new Vector2(-18f, -14f));
        SetRect(
            mapViewport,
            new Vector2(0f, 0.36f),
            new Vector2(1f, 0.94f),
            new Vector2(12f, 8f),
            new Vector2(-12f, -8f));
        SetRect(
            detail,
            new Vector2(0f, 0.17f),
            new Vector2(1f, 0.35f),
            new Vector2(18f, 8f),
            new Vector2(-18f, -6f));
        SetRect(
            actionViewport,
            new Vector2(0f, 0f),
            new Vector2(1f, 0.16f),
            new Vector2(18f, 12f),
            new Vector2(-18f, -6f));
        ConfigureDetailText(19f, 14f);
    }

    private void ConfigureCanvas(Vector2 referenceResolution)
    {
        if (canvasScaler == null
            || canvasScaler.referenceResolution == referenceResolution)
        {
            return;
        }

        canvasScaler.referenceResolution = referenceResolution;
        canvasScaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0f;
    }

    private void ConfigureDetailText(float maxSize, float minSize)
    {
        TMP_Text text = detail.GetComponent<TMP_Text>();
        if (text == null)
        {
            return;
        }

        text.enableAutoSizing = true;
        text.fontSizeMax = maxSize;
        text.fontSizeMin = minSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
