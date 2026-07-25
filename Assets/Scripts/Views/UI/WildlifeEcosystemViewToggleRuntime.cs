using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

public sealed class WildlifeEcosystemViewToggleRuntime :
    IStartable,
    IDisposable
{
    private readonly IWildlifeEcosystemRuntime ecosystemRuntime;
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IDungeonUiCanvasProvider canvasProvider;
    private readonly ITmpKoreanFontService fontService;

    private GridSystemManager gridSystemManager;
    private Button toggleButton;

    public WildlifeEcosystemViewToggleRuntime(
        IWildlifeEcosystemRuntime ecosystemRuntime,
        IGridSystemProvider gridSystemProvider,
        IDungeonUiCanvasProvider canvasProvider,
        ITmpKoreanFontService fontService)
    {
        this.ecosystemRuntime = ecosystemRuntime
            ?? throw new ArgumentNullException(nameof(ecosystemRuntime));
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.canvasProvider = canvasProvider
            ?? throw new ArgumentNullException(nameof(canvasProvider));
        this.fontService = fontService
            ?? throw new ArgumentNullException(nameof(fontService));
    }

    public void Start()
    {
        gridSystemProvider.TryGetManager(out gridSystemManager);
        Canvas canvas = canvasProvider.GetOrCreateCanvas();
        RectTransform upperRightPanel = canvas
            .GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(item =>
                item != null && item.name == "UpperRightPanel");
        if (upperRightPanel == null)
        {
            return;
        }

        toggleButton = CreateToggleButton(upperRightPanel);
        toggleButton.transform.SetSiblingIndex(0);
        toggleButton.onClick.AddListener(Toggle);

        if (gridSystemManager != null)
        {
            gridSystemManager.OnGridModeChanged += OnGridModeChanged;
            toggleButton.interactable = gridSystemManager.Mode == GridMode.None;
        }

        RefreshVisual();
    }

    public void Dispose()
    {
        if (gridSystemManager != null)
        {
            gridSystemManager.OnGridModeChanged -= OnGridModeChanged;
        }

        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(Toggle);
            UnityEngine.Object.Destroy(toggleButton.gameObject);
        }
    }

    private void Toggle()
    {
        ecosystemRuntime.SetOverlayEnabled(!ecosystemRuntime.OverlayEnabled);
        RefreshVisual();
    }

    private void OnGridModeChanged(GridMode mode)
    {
        if (toggleButton != null)
        {
            toggleButton.interactable = mode == GridMode.None;
        }

        if (mode != GridMode.None)
        {
            ecosystemRuntime.SetOverlayEnabled(false);
        }

        RefreshVisual();
    }

    private Button CreateToggleButton(RectTransform parent)
    {
        GameObject buttonObject = new GameObject(
            "WildlifeEcosystemViewToggle",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(120f, 120f);

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.text = "야생";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 18f;
        label.fontSizeMin = 13f;
        label.fontSizeMax = 18f;
        label.enableAutoSizing = true;
        label.characterSpacing = 0f;
        fontService.Apply(label);

        Button button = buttonObject.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, false);
        return button;
    }

    private void RefreshVisual()
    {
        if (toggleButton != null)
        {
            DungeonUiTheme.StyleButton(
                toggleButton,
                ecosystemRuntime.OverlayEnabled);
        }
    }
}
