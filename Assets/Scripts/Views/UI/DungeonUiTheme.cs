using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class DungeonUiTheme
{
    private static readonly Color StandardCanvasScrim = Hex("40565BFF");
    private static readonly Color StandardPanel = Hex("435D64FF");
    private static readonly Color StandardSurface = Hex("567179FF");
    private static readonly Color StandardSurfaceRaised = Hex("789198FF");
    private static readonly Color StandardSurfaceMuted = Hex("34484EFF");
    private static readonly Color StandardBorder = Hex("A9BEC2FF");
    private static readonly Color StandardTextPrimary = Hex("F5F7F4FF");
    private static readonly Color StandardTextSecondary = Hex("D2DDD9FF");
    private static readonly Color StandardAccent = Hex("56B892FF");

    public static Color CanvasScrim => IsHighContrast ? Hex("182428FF") : StandardCanvasScrim;
    public static Color Panel => IsHighContrast ? Hex("172126FF") : StandardPanel;
    public static Color Surface => IsHighContrast ? Hex("223137FF") : StandardSurface;
    public static Color SurfaceRaised => IsHighContrast ? Hex("344951FF") : StandardSurfaceRaised;
    public static Color SurfaceMuted => IsHighContrast ? Hex("10191DFF") : StandardSurfaceMuted;
    public static Color Border => IsHighContrast ? Hex("D8E4E1FF") : StandardBorder;
    public static Color TextPrimary => IsHighContrast ? Color.white : StandardTextPrimary;
    public static Color TextSecondary => IsHighContrast ? Hex("D8E4E1FF") : StandardTextSecondary;
    public static Color Accent => IsHighContrast ? Hex("46D69BFF") : StandardAccent;
    public static Color AccentHover => IsHighContrast ? Hex("79F0BFFF") : Hex("8CE0BFFF");
    public static Color AccentPressed => IsHighContrast ? Hex("2CA879FF") : Hex("3E8B70FF");
    public static Color Warning => IsHighContrast ? Hex("FFD15AFF") : Hex("D2A449FF");
    public static Color Danger => IsHighContrast ? Hex("FF7770FF") : Hex("C95E5AFF");
    public static Color Good => IsHighContrast ? Hex("55E6A7FF") : Hex("4CB88BFF");
    public static float ModalScrimAlpha => IsHighContrast ? 0.56f : 0.34f;
    public static float OwnerSelectionScrimAlpha => IsHighContrast ? 0.62f : 0.42f;
    public static float ResultScrimAlpha => IsHighContrast ? 0.60f : 0.40f;
    public static Color ModalScrim => WithAlpha(CanvasScrim, ModalScrimAlpha);
    public static Color OwnerSelectionScrim => WithAlpha(CanvasScrim, OwnerSelectionScrimAlpha);
    public static Color ResultScrim => WithAlpha(CanvasScrim, ResultScrimAlpha);

    private static bool IsHighContrast => DungeonUserSettingsRuntime.Current.highContrast;

    public static Color GetMeterColor(float normalizedValue)
    {
        if (normalizedValue < 0.25f) return Danger;
        if (normalizedValue < 0.5f) return Warning;
        return Good;
    }

    public static void StyleButton(Button button, bool selected = false, bool destructive = false)
    {
        if (button == null) return;

        Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (image == null) return;

        Color normal = destructive ? Danger : selected ? Accent : SurfaceRaised;
        image.color = normal;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = destructive ? Hex("FFBAB7FF") : selected ? Hex("C5F0DEFF") : Hex("C8D7DAFF"),
            pressedColor = destructive ? Hex("C47B78FF") : selected ? Hex("8FCDB5FF") : Hex("91A5AAFF"),
            selectedColor = Color.white,
            disabledColor = Hex("3F4E53CC"),
            colorMultiplier = 1f,
            fadeDuration = DungeonUserSettingsRuntime.Current.reducedMotion ? 0f : 0.08f
        };

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = TextPrimary;
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 0f;
        }
    }

    private static Color Hex(string html)
    {
        if (!ColorUtility.TryParseHtmlString("#" + html, out Color color))
        {
            throw new InvalidOperationException($"Invalid UI theme color: {html}");
        }

        return color;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
    }
}

[DisallowMultipleComponent]
public sealed class DungeonUiThemeRuntime : MonoBehaviour
{
    private const float RefreshInterval = 0.4f;

    private ITmpKoreanFontService fontService;
    private IUiClock uiClock;
    private Canvas targetCanvas;
    private TabId? activeTabId;
    private float nextRefreshAt;
    private readonly Dictionary<int, TextScaleBaseline> textScaleBaselines =
        new Dictionary<int, TextScaleBaseline>();
    private readonly List<Transform> transformBuffer = new List<Transform>();
    private readonly List<Button> buttonBuffer = new List<Button>();
    private readonly List<Image> imageBuffer = new List<Image>();
    private readonly List<TMP_Text> textBuffer = new List<TMP_Text>();
    private readonly HashSet<int> liveTextIds = new HashSet<int>();
    private readonly List<int> staleTextIds = new List<int>();
    private int observedHierarchyCount = -1;
    private int observedScreenWidth = -1;
    private int observedScreenHeight = -1;
    private float observedUiScale = -1f;
    private float observedTextScale = -1f;

    public static DungeonUiThemeRuntime Ensure(
        Canvas canvas,
        ITmpKoreanFontService fontService,
        IUiClock uiClock)
    {
        if (canvas == null)
        {
            throw new ArgumentNullException(nameof(canvas));
        }

        DungeonUiThemeRuntime runtime = canvas.GetComponent<DungeonUiThemeRuntime>();
        if (runtime == null)
        {
            runtime = canvas.gameObject.AddComponent<DungeonUiThemeRuntime>();
        }

        runtime.targetCanvas = canvas;
        runtime.fontService = fontService;
        runtime.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        runtime.ApplyNow();
        return runtime;
    }

    public void SetActiveTab(TabId? tabId)
    {
        activeTabId = tabId;
        StyleBottomNavigation();
    }

    public void ApplyNow()
    {
        if (targetCanvas == null)
        {
            targetCanvas = GetComponent<Canvas>();
        }

        if (targetCanvas == null) return;

        CaptureRefreshSignature();
        ConfigureCanvasScaler();
        ApplyFonts();
        StyleTopHud();
        StyleBottomNavigation();
        StyleLegacyPanels();
        ApplyTextScale();
    }

    private void Update()
    {
        if (uiClock == null)
        {
            return;
        }

        if (uiClock.Time < nextRefreshAt) return;

        nextRefreshAt = uiClock.Time + RefreshInterval;
        if (HasRefreshSignatureChanged())
        {
            ApplyNow();
        }
    }

    private void ConfigureCanvasScaler()
    {
        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler == null) return;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        float uiScale = Mathf.Clamp(DungeonUserSettingsRuntime.Current.uiScale, 0.8f, 1.25f);
        scaler.referenceResolution = new Vector2(1920f / uiScale, 1080f / uiScale);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void StyleTopHud()
    {
        Transform root = targetCanvas.transform;
        StyleTimeBlock(root.Find("Time"));
        StyleMoneyBlock(FindDirectChild(root, "Panel"));
        StyleUpperRightControls(root.Find("UpperRightPanel"));
    }

    private void StyleTimeBlock(Transform block)
    {
        if (!(block is RectTransform rect)) return;

        SetTopLeft(rect, new Vector2(24f, -24f), new Vector2(260f, 56f));
        StyleBlockImage(block, DungeonUiTheme.Panel);

        TMP_Text label = block.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;

        label.color = DungeonUiTheme.TextPrimary;
        label.fontSize = 25f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 25f;
        SetStretch(label.rectTransform, new Vector2(16f, 4f), new Vector2(-12f, -4f));
    }

    private void StyleMoneyBlock(Transform block)
    {
        if (!(block is RectTransform rect)) return;

        SetTopLeft(rect, new Vector2(24f, -88f), new Vector2(300f, 64f));
        StyleBlockImage(block, DungeonUiTheme.Panel);

        Image icon = null;
        TMP_Text amount = null;
        foreach (Transform child in block)
        {
            icon ??= child.GetComponent<Image>();
            amount ??= child.GetComponent<TMP_Text>();
        }

        if (icon != null && icon.transform is RectTransform iconRect)
        {
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(38f, 0f);
            iconRect.sizeDelta = new Vector2(48f, 48f);
            icon.preserveAspect = true;
        }

        if (amount != null)
        {
            amount.color = DungeonUiTheme.TextPrimary;
            amount.fontSize = 28f;
            amount.fontStyle = FontStyles.Bold;
            amount.alignment = TextAlignmentOptions.MidlineRight;
            SetStretch(amount.rectTransform, new Vector2(76f, 4f), new Vector2(-18f, -4f));
        }
    }

    private void StyleUpperRightControls(Transform controls)
    {
        if (!(controls is RectTransform rect)) return;

        buttonBuffer.Clear();
        controls.GetComponentsInChildren(true, buttonBuffer);
        List<Button> buttons = buttonBuffer;
        const float widthForThreeButtons = 292f;
        float desiredPanelWidth = widthForThreeButtons
            * Mathf.Max(3, buttons.Count)
            / 3f;
        float canvasWidth = GetCanvasSize().x;
        float panelWidth = Mathf.Min(
            desiredPanelWidth,
            Mathf.Max(240f, canvasWidth - 48f));

        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-24f, -24f);
        rect.sizeDelta = new Vector2(panelWidth, 56f);
        StyleBlockImage(controls, new Color(0f, 0f, 0f, 0f));

        LayoutGroup existingLayout = controls.GetComponent<LayoutGroup>();
        if (existingLayout != null)
        {
            existingLayout.enabled = false;
        }

        for (int index = 0; index < buttons.Count; index++)
        {
            Button button = buttons[index];
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                float left = index / (float)Mathf.Max(1, buttons.Count);
                float right = (index + 1f) / Mathf.Max(1, buttons.Count);
                buttonRect.anchorMin = new Vector2(left, 0f);
                buttonRect.anchorMax = new Vector2(right, 1f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.offsetMin = new Vector2(index > 0 ? 3f : 0f, 0f);
                buttonRect.offsetMax = new Vector2(index < buttons.Count - 1 ? -3f : 0f, 0f);
            }

            RoomInspectionToggleVisualState selectionState =
                button.GetComponent<RoomInspectionToggleVisualState>();
            Image buttonImage = button.targetGraphic as Image ?? button.GetComponent<Image>();
            bool selected = selectionState != null
                ? selectionState.IsSelected
                : buttonImage != null && ColorsMatch(buttonImage.color, DungeonUiTheme.Accent);
            DungeonUiTheme.StyleButton(button, selected);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.fontSize = 18f;
                label.enableAutoSizing = true;
                label.fontSizeMin = panelWidth < desiredPanelWidth ? 9f : 12f;
                label.fontSizeMax = 18f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }
    }

    private static bool ColorsMatch(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f
            && Mathf.Abs(a.g - b.g) < 0.01f
            && Mathf.Abs(a.b - b.b) < 0.01f
            && Mathf.Abs(a.a - b.a) < 0.01f;
    }

    private void StyleBottomNavigation()
    {
        Transform navigation = targetCanvas != null ? targetCanvas.transform.Find("TabButtons") : null;
        if (!(navigation is RectTransform rect)) return;

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 64f);
        StyleBlockImage(navigation, DungeonUiTheme.Panel);

        HorizontalLayoutGroup layout = navigation.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(8, 8, 7, 7);
            layout.spacing = 3f;
        }

        buttonBuffer.Clear();
        navigation.GetComponentsInChildren(true, buttonBuffer);
        List<Button> buttons = buttonBuffer;
        for (int index = 0; index < buttons.Count; index++)
        {
            Button button = buttons[index];
            LayoutElement layoutElement = button.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = button.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minWidth = 0f;
            layoutElement.preferredWidth = 0f;
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = 0f;
            layoutElement.preferredHeight = 0f;
            layoutElement.flexibleHeight = 1f;

            UITabButtonBinding binding = button.GetComponent<UITabButtonBinding>();
            bool selected = activeTabId.HasValue
                && binding != null
                && binding.Id == activeTabId.Value;
            DungeonUiTheme.StyleButton(button, selected);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.fontSize = 18f;
                label.enableAutoSizing = true;
                label.fontSizeMin = GetCanvasSize().x < 1200f ? 8f : 12f;
                label.fontSizeMax = 18f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }
    }

    private void StyleLegacyPanels()
    {
        Transform root = targetCanvas.transform;
        foreach (Transform child in root)
        {
            if (child.name == "BuildingInfoPanel")
            {
                StyleBuildingPanel(child);
            }
        }

        Transform constructTab = root.Find("ConstructTab");
        if (constructTab != null)
        {
            imageBuffer.Clear();
            constructTab.GetComponentsInChildren(true, imageBuffer);
            foreach (Image image in imageBuffer)
            {
                if (image.GetComponent<Button>() == null && image.sprite != null && image.gameObject.name == "Image")
                {
                    continue;
                }

                if (image.GetComponent<Button>() == null)
                {
                    image.color = DungeonUiTheme.SurfaceMuted;
                }
            }

            buttonBuffer.Clear();
            constructTab.GetComponentsInChildren(true, buttonBuffer);
            foreach (Button button in buttonBuffer)
            {
                DungeonUiTheme.StyleButton(button);
            }
        }
    }

    private void StyleBuildingPanel(Transform panel)
    {
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = DungeonUiTheme.Panel;
        }

        UIBuildingInfo buildingInfo = panel.GetComponent<UIBuildingInfo>();
        GameObject previewObject = buildingInfo != null ? buildingInfo.buildingImageObject : null;

        imageBuffer.Clear();
        panel.GetComponentsInChildren(true, imageBuffer);
        foreach (Image image in imageBuffer)
        {
            if (image.GetComponent<Button>() != null) continue;
            if (previewObject != null && image.gameObject == previewObject)
            {
                if (image.sprite != null)
                {
                    image.color = Color.white;
                }

                continue;
            }

            if (image.sprite == null || image.sprite.name == "Background")
            {
                image.color = image.gameObject.name == "UpperPanel"
                    ? DungeonUiTheme.SurfaceRaised
                    : DungeonUiTheme.Surface;
            }
        }

        buttonBuffer.Clear();
        panel.GetComponentsInChildren(true, buttonBuffer);
        foreach (Button button in buttonBuffer)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            bool destructive = label != null && label.text.Contains("부시");
            DungeonUiTheme.StyleButton(button, destructive: destructive);
        }

        textBuffer.Clear();
        panel.GetComponentsInChildren(true, textBuffer);
        foreach (TMP_Text label in textBuffer)
        {
            label.color = DungeonUiTheme.TextPrimary;
            label.characterSpacing = 0f;
        }
    }

    private static Transform FindDirectChild(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
        }

        return null;
    }

    private void ApplyTextScale()
    {
        float scale = Mathf.Clamp(DungeonUserSettingsRuntime.Current.textScale, 0.9f, 1.25f);
        textBuffer.Clear();
        targetCanvas.GetComponentsInChildren(true, textBuffer);
        liveTextIds.Clear();
        foreach (TMP_Text text in textBuffer)
        {
            if (text == null)
            {
                continue;
            }

            int id = text.GetInstanceID();
            liveTextIds.Add(id);
            if (!textScaleBaselines.TryGetValue(id, out TextScaleBaseline baseline))
            {
                baseline = new TextScaleBaseline(text.fontSize, text.fontSizeMin, text.fontSizeMax);
                textScaleBaselines.Add(id, baseline);
            }

            text.fontSize = baseline.FontSize * scale;
            text.fontSizeMin = baseline.FontSizeMin * scale;
            text.fontSizeMax = baseline.FontSizeMax * scale;
        }

        staleTextIds.Clear();
        foreach (int id in textScaleBaselines.Keys)
        {
            if (!liveTextIds.Contains(id))
            {
                staleTextIds.Add(id);
            }
        }

        for (int index = 0; index < staleTextIds.Count; index++)
        {
            textScaleBaselines.Remove(staleTextIds[index]);
        }
    }

    private void ApplyFonts()
    {
        if (fontService == null)
        {
            return;
        }

        textBuffer.Clear();
        targetCanvas.GetComponentsInChildren(true, textBuffer);
        foreach (TMP_Text text in textBuffer)
        {
            fontService.Apply(text);
        }
    }

    private bool HasRefreshSignatureChanged()
    {
        if (targetCanvas == null)
        {
            return false;
        }

        transformBuffer.Clear();
        targetCanvas.GetComponentsInChildren(true, transformBuffer);
        DungeonUserSettingsData settings = DungeonUserSettingsRuntime.Current;
        return transformBuffer.Count != observedHierarchyCount
            || Screen.width != observedScreenWidth
            || Screen.height != observedScreenHeight
            || !Mathf.Approximately(settings.uiScale, observedUiScale)
            || !Mathf.Approximately(settings.textScale, observedTextScale);
    }

    private void CaptureRefreshSignature()
    {
        transformBuffer.Clear();
        targetCanvas.GetComponentsInChildren(true, transformBuffer);
        DungeonUserSettingsData settings = DungeonUserSettingsRuntime.Current;
        observedHierarchyCount = transformBuffer.Count;
        observedScreenWidth = Screen.width;
        observedScreenHeight = Screen.height;
        observedUiScale = settings.uiScale;
        observedTextScale = settings.textScale;
    }

    private readonly struct TextScaleBaseline
    {
        public TextScaleBaseline(float fontSize, float fontSizeMin, float fontSizeMax)
        {
            FontSize = fontSize;
            FontSizeMin = fontSizeMin;
            FontSizeMax = fontSizeMax;
        }

        public float FontSize { get; }
        public float FontSizeMin { get; }
        public float FontSizeMax { get; }
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void StyleBlockImage(Transform target, Color color)
    {
        Image image = target != null ? target.GetComponent<Image>() : null;
        if (image != null)
        {
            image.color = color;
        }
    }

    private Vector2 GetCanvasSize()
    {
        if (targetCanvas != null && targetCanvas.transform is RectTransform rect)
        {
            return rect.rect.size;
        }

        return new Vector2(Screen.width, Screen.height);
    }
}
