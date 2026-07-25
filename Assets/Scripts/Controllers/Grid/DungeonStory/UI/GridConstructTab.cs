using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class GridConstructTab : UITab
{
    private const float CompactCatalogHeight = 132f;
    private const float CatalogPadding = 8f;
    private const float PanelGap = 8f;
    private const float CategoryWidthRatio = 0.46f;
    private const float MinimumCategoryWidth = 800f;
    private const float MaximumCategoryWidth = 920f;
    private const int CategoryColumnCount = 8;
    private const float CategorySpacing = 6f;
    private const float ItemCellSize = 88f;
    private const float ItemSpacing = 6f;
    private const string ItemScrollContentName = "ItemScrollContent";

    [Tooltip("Category button prefab")]
    public GameObject buildingCategorySelectButtonPrefab;
    [Tooltip("Building select panel prefab")]
    public GameObject seleButtonPanelPrefab;
    [Tooltip("Building select button prefab")]
    public GameObject selectButtonPrefab;

    public List<UITab> selectButtonPanelList;
    private IDataCatalog dataCatalog;
    private IUiPopupService popupService;
    private IDungeonGridBuildingControllerProvider buildingControllerProvider;
    private IBlueprintResearchRuntimeProvider researchRuntimeProvider;
    private ITmpKoreanFontService tmpKoreanFontService;
    private IGridConstructButtonFactory buttonFactory;
    private bool preservePlacementOnClose;

    [Inject]
    public void ConstructGridConstructTab(
        IDataCatalog dataCatalog,
        IUiPopupService popupService,
        IDungeonGridBuildingControllerProvider buildingControllerProvider,
        IBlueprintResearchRuntimeProvider researchRuntimeProvider,
        ITmpKoreanFontService tmpKoreanFontService,
        IGridConstructButtonFactory buttonFactory)
    {
        this.dataCatalog = dataCatalog ?? throw new ArgumentNullException(nameof(dataCatalog));
        this.popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        this.buildingControllerProvider = buildingControllerProvider
            ?? throw new ArgumentNullException(nameof(buildingControllerProvider));
        this.researchRuntimeProvider = researchRuntimeProvider
            ?? throw new ArgumentNullException(nameof(researchRuntimeProvider));
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
        this.buttonFactory = buttonFactory ?? throw new ArgumentNullException(nameof(buttonFactory));
    }

    private void Start()
    {
        MakeSelectButton();
        ConfigureCompactLayout();
    }

    public override void OnClose()
    {
        bool preservePlacement = preservePlacementOnClose;
        preservePlacementOnClose = false;
        base.OnClose();
        if (!preservePlacement)
        {
            RequireBuildingController().SetGridModeNone();
        }
    }

    public override void OnOpen()
    {
        RequireBuildingController().SetGridModeNone();
        base.OnOpen();
        RequirePopupService().CloseAll();
        MakeSelectButton();
        RefreshCategoryLabels();
        ConfigureCompactLayout();
    }

    public void CollapseForPlacement()
    {
        foreach (UITab panel in selectButtonPanelList ?? Enumerable.Empty<UITab>())
        {
            if (panel != null && panel.gameObject.activeSelf)
            {
                panel.CloseTab();
            }
        }

        preservePlacementOnClose = true;
        CloseTab();
    }

    private void MakeSelectButton()
    {
        selectButtonPanelList ??= new List<UITab>();

        foreach (BuildingSO building in RequireDataCatalog()
                     .GetData<BuildingSO>()
                     .Values
                     .Where(IsVisibleBuilding)
                     .OrderBy((x) => x.id))
        {
            BuildingCategory menuCategory = ResolveMenuCategory(building);
            if (!HasCategoryPanel(menuCategory))
            {
                AddCategoryPanel(menuCategory);
            }

            Transform selectPanel = GetCategoryPanelContent(menuCategory);
            if (!HasBuildingSelectButton(selectPanel, building.id))
            {
                RequireButtonFactory().CreateBuildingSelectButton(selectButtonPrefab, selectPanel, building);
            }
        }

        foreach (UITab panel in selectButtonPanelList)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(false);
            }
        }

        RefreshCategoryLabels();
        ConfigureCompactLayout();
    }

    private void ConfigureCompactLayout()
    {
        if (transform is not RectTransform root)
        {
            return;
        }

        float previousBottom = root.anchoredPosition.y - (root.rect.height * root.pivot.y);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, CompactCatalogHeight);
        Vector2 rootPosition = root.anchoredPosition;
        rootPosition.y = previousBottom + (CompactCatalogHeight * root.pivot.y);
        root.anchoredPosition = rootPosition;

        float rootWidth = root.rect.width;
        float contentHeight = CompactCatalogHeight - (CatalogPadding * 2f);
        float categoryWidth = Mathf.Clamp(
            rootWidth * CategoryWidthRatio,
            MinimumCategoryWidth,
            MaximumCategoryWidth);

        ConfigureCategoryLayout(categoryWidth, contentHeight);
        ConfigureItemLayouts(rootWidth, categoryWidth, contentHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    private void ConfigureCategoryLayout(float width, float height)
    {
        if (ResolveCategoryButtonRoot() is not RectTransform categoryRoot)
        {
            return;
        }

        SetBottomLeftRect(categoryRoot, CatalogPadding, CatalogPadding, width, height);

        GridLayoutGroup layout = categoryRoot.GetComponent<GridLayoutGroup>();
        if (layout == null)
        {
            return;
        }

        int buttonCount = Mathf.Max(1, categoryRoot.childCount);
        int rowCount = Mathf.Max(1, Mathf.CeilToInt(buttonCount / (float)CategoryColumnCount));
        float innerWidth = width - layout.padding.horizontal;
        float innerHeight = height - layout.padding.vertical;
        float cellWidth = (innerWidth - (CategorySpacing * (CategoryColumnCount - 1))) / CategoryColumnCount;
        float cellHeight = (innerHeight - (CategorySpacing * (rowCount - 1))) / rowCount;

        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = CategoryColumnCount;
        layout.spacing = new Vector2(CategorySpacing, CategorySpacing);
        layout.cellSize = new Vector2(Mathf.Max(1f, cellWidth), Mathf.Max(1f, cellHeight));
    }

    private void ConfigureItemLayouts(float rootWidth, float categoryWidth, float height)
    {
        float left = CatalogPadding + categoryWidth + PanelGap;
        float width = Mathf.Max(ItemCellSize, rootWidth - left - CatalogPadding);

        foreach (UITab panel in selectButtonPanelList ?? Enumerable.Empty<UITab>())
        {
            if (panel == null || panel.transform is not RectTransform panelRect)
            {
                continue;
            }

            SetBottomLeftRect(panelRect, left, CatalogPadding, width, height);

            RectTransform content = EnsureItemScroll(panel);
            GridLayoutGroup layout = content.GetComponent<GridLayoutGroup>();
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            layout.constraintCount = 1;
            layout.spacing = new Vector2(ItemSpacing, ItemSpacing);
            layout.cellSize = new Vector2(ItemCellSize, ItemCellSize);

            int itemCount = content.childCount;
            float naturalContentWidth = layout.padding.horizontal
                + itemCount * ItemCellSize
                + Mathf.Max(0, itemCount - 1) * ItemSpacing;
            float trailingScrollRoom = naturalContentWidth > width
                ? Mathf.Max(0f, width - ItemCellSize)
                : 0f;
            float contentWidth = naturalContentWidth + trailingScrollRoom;
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(width, contentWidth));
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }

    private static void SetBottomLeftRect(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private bool HasBuildingSelectButton(Transform selectPanel, int buildingId)
    {
        if (selectPanel == null)
        {
            return false;
        }

        foreach (UIBuildingSelectButton button in selectPanel.GetComponentsInChildren<UIBuildingSelectButton>(true))
        {
            if (button == null || button.gameObject == selectButtonPrefab)
            {
                continue;
            }

            if (button.id == buildingId)
            {
                return true;
            }
        }

        return false;
    }

    private void AddCategoryPanel(BuildingCategory category)
    {
        int categoryId = (int)category;
        UITab panel = RequireButtonFactory().CreateCategoryPanel(
            seleButtonPanelPrefab,
            gameObject.transform,
            category);
        EnsureItemScroll(panel);
        RequireButtonFactory().CreateCategoryButton(
            buildingCategorySelectButtonPrefab,
            ResolveCategoryButtonRoot(),
            GetCategoryDisplayName(category),
            () => ToggleSelectButton(categoryId));
        selectButtonPanelList.Add(panel);
    }

    private bool HasCategoryPanel(BuildingCategory category)
    {
        int categoryId = (int)category;
        return selectButtonPanelList.Any((x) => x != null && x.id == categoryId);
    }

    private UITab GetCategoryPanel(BuildingCategory category)
    {
        int categoryId = (int)category;
        UITab panel = selectButtonPanelList.FirstOrDefault((x) => x != null && x.id == categoryId);
        if (panel == null)
        {
            throw new InvalidOperationException($"{nameof(GridConstructTab)} could not find category tab {categoryId}.");
        }

        return panel;
    }

    private Transform GetCategoryPanelContent(BuildingCategory category)
    {
        return EnsureItemScroll(GetCategoryPanel(category));
    }

    private static RectTransform EnsureItemScroll(UITab panel)
    {
        if (panel == null || panel.transform is not RectTransform panelRect)
        {
            throw new InvalidOperationException("Building category panel requires a RectTransform.");
        }

        GridLayoutGroup legacyLayout = panel.GetComponent<GridLayoutGroup>();
        if (legacyLayout != null)
        {
            legacyLayout.enabled = false;
        }

        ScrollRect scrollRect = panel.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = panel.gameObject.AddComponent<ScrollRect>();
        }

        RectMask2D mask = panel.GetComponent<RectMask2D>();
        if (mask == null)
        {
            panel.gameObject.AddComponent<RectMask2D>();
        }

        Image viewportImage = panel.GetComponent<Image>();
        if (viewportImage == null)
        {
            viewportImage = panel.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
        }
        viewportImage.raycastTarget = true;

        Transform existingContent = panel.transform.Find(ItemScrollContentName);
        RectTransform content;
        if (existingContent is RectTransform existingRect)
        {
            content = existingRect;
        }
        else
        {
            GameObject contentObject = new GameObject(
                ItemScrollContentName,
                typeof(RectTransform),
                typeof(GridLayoutGroup));
            contentObject.transform.SetParent(panel.transform, false);
            content = contentObject.GetComponent<RectTransform>();

            List<Transform> existingChildren = new List<Transform>();
            for (int index = 0; index < panel.transform.childCount; index++)
            {
                Transform child = panel.transform.GetChild(index);
                if (child != content)
                {
                    existingChildren.Add(child);
                }
            }

            foreach (Transform child in existingChildren)
            {
                child.SetParent(content, false);
            }
        }

        if (content.GetComponent<GridLayoutGroup>() == null)
        {
            content.gameObject.AddComponent<GridLayoutGroup>();
        }

        scrollRect.viewport = panelRect;
        scrollRect.content = content;
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 32f;
        return content;
    }

    private Transform ResolveCategoryButtonRoot()
    {
        return transform.childCount > 0 ? transform.GetChild(0) : transform;
    }

    public void ToggleSelectButton(int category)
    {
        UITab temp = null;
        foreach (UITab tab in selectButtonPanelList)
        {
            if (tab == null) continue;

            if (tab.id == category) temp = tab;
            else tab.CloseTab();
        }

        if (temp == null)
        {
            throw new InvalidOperationException($"{nameof(GridConstructTab)} could not find category tab {category}.");
        }

        RequireBuildingController().SetGridModeNone();
        ConfigureCompactLayout();
        temp.Toggle();
    }

    public void RefreshCategoryLabels()
    {
        foreach (TMP_Text label in transform.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label == null) continue;

            if (TryGetCategoryDisplayName(label.text, out string displayName))
            {
                RequireTmpKoreanFontService().Apply(label);
                label.text = displayName;
            }
        }
    }

    private static string GetCategoryDisplayName(BuildingCategory category)
    {
        return BuildingCategoryCatalog.GetDisplayName(category, "기타");
    }

    private static bool TryGetCategoryDisplayName(string rawText, out string displayName)
    {
        displayName = string.Empty;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return false;
        }

        string normalized = rawText.Trim();
        if (BuildingCategoryCatalog.TryResolve(
                normalized,
                out BuildingCategoryDefinition definition))
        {
            displayName = definition.DisplayName;
            return true;
        }

        return false;
    }

    private static BuildingCategory ResolveMenuCategory(BuildingSO building)
    {
        return building != null && building.IsInteriorDoor
            ? BuildingCategory.Wall
            : building != null
                ? building.category
                : BuildingCategory.None;
    }

    private bool IsVisibleBuilding(BuildingSO building)
    {
        if (building == null)
        {
            return false;
        }

        if (building.unlocked)
        {
            return true;
        }

        return researchRuntimeProvider != null
            && researchRuntimeProvider.TryGetRuntime(out BlueprintResearchRuntime runtime)
            && runtime.State.IsBuildingUnlocked(building.id);
    }

    private IDataCatalog RequireDataCatalog()
    {
        return dataCatalog
            ?? throw new InvalidOperationException($"{nameof(GridConstructTab)} requires {nameof(IDataCatalog)} injection.");
    }

    private IUiPopupService RequirePopupService()
    {
        return popupService
            ?? throw new InvalidOperationException($"{nameof(GridConstructTab)} requires {nameof(IUiPopupService)} injection.");
    }

    private IDungeonGridBuildingControllerProvider RequireBuildingControllerProvider()
    {
        return buildingControllerProvider
            ?? throw new InvalidOperationException($"{nameof(GridConstructTab)} requires {nameof(IDungeonGridBuildingControllerProvider)} injection.");
    }

    private DungeonStoryGridBuildingController RequireBuildingController()
    {
        return RequireBuildingControllerProvider().Controller;
    }

    private ITmpKoreanFontService RequireTmpKoreanFontService()
    {
        return tmpKoreanFontService
            ?? throw new InvalidOperationException(
                $"{nameof(GridConstructTab)} requires {nameof(ITmpKoreanFontService)} injection.");
    }

    private IGridConstructButtonFactory RequireButtonFactory()
    {
        return buttonFactory
            ?? throw new InvalidOperationException(
                $"{nameof(GridConstructTab)} requires {nameof(IGridConstructButtonFactory)} injection.");
    }
}
