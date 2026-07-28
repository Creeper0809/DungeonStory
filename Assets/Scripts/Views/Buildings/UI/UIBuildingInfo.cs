using DG.Tweening;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class UIBuildingInfo : SerializedMonoBehaviour
{
    private BuildableObject selectedBuilding;
    private CanvasGroup canvasGroup;
    private Image buildingImage;
    private RectTransform buildingImageSize;
    private IBuildingDefinitionLookup buildingDefinitionLookup;
    private IBuildingSummaryFormatter summaryFormatter;
    private IUiPopupService popupService;
    private ICombatEquipmentMaintenanceRuntime equipmentMaintenanceRuntime;
    private ICombatEquipmentRuntime combatEquipmentRuntime;
    private ICombatEquipmentCatalog combatEquipmentCatalog;
    private IWorkOrderRuntime workOrderRuntime;
    private IGameEventBus gameEventBus;
    private IDoorAccessPanelPresenter doorAccessPanelPresenter;
    private ICircusBuildingPanelPresenter circusBuildingPanelPresenter;
    private IEquipmentCraftingPanelPresenter equipmentCraftingPanelPresenter;
    private IInstanceEvolutionPanelPresenter instanceEvolutionPanelPresenter;
    private IProductionBuildingPanelPresenter productionBuildingPanelPresenter;
    private ICropPlotBuildingPanelPresenter cropPlotBuildingPanelPresenter;
    private IAnimalHusbandryBuildingPanelPresenter animalHusbandryPanelPresenter;
    private IDisposable infoFeedSubscription;
    private bool initialized;
    private readonly List<GameObject> craftActionObjects = new List<GameObject>();
    private string craftStatusMessage = string.Empty;
    private GameObject contextActionsPanel;
    private RectTransform contextActionsContent;

    public GameObject buildingImageObject;

    public List<UIConfig<TMP_Text>> simpleInfoText;
    public TMP_Text nameText;

    public GameObject textPrefab;
    public GameObject simpleInfoPanel;

    private bool hidden = true;

    [Inject]
    public void ConstructUIBuildingInfo(
        IBuildingDefinitionLookup buildingDefinitionLookup,
        IBuildingSummaryFormatter summaryFormatter,
        IUiPopupService popupService,
        ICombatEquipmentMaintenanceRuntime equipmentMaintenanceRuntime,
        ICombatEquipmentRuntime combatEquipmentRuntime,
        ICombatEquipmentCatalog combatEquipmentCatalog,
        IWorkOrderRuntime workOrderRuntime,
        IDoorAccessPanelPresenter doorAccessPanelPresenter,
        ICircusBuildingPanelPresenter circusBuildingPanelPresenter,
        IEquipmentCraftingPanelPresenter equipmentCraftingPanelPresenter,
        IInstanceEvolutionPanelPresenter instanceEvolutionPanelPresenter,
        IProductionBuildingPanelPresenter productionBuildingPanelPresenter,
        ICropPlotBuildingPanelPresenter cropPlotBuildingPanelPresenter,
        IAnimalHusbandryBuildingPanelPresenter animalHusbandryPanelPresenter)
    {
        this.buildingDefinitionLookup = buildingDefinitionLookup
            ?? throw new ArgumentNullException(nameof(buildingDefinitionLookup));
        this.summaryFormatter = summaryFormatter
            ?? throw new ArgumentNullException(nameof(summaryFormatter));
        this.popupService = popupService
            ?? throw new ArgumentNullException(nameof(popupService));
        this.equipmentMaintenanceRuntime = equipmentMaintenanceRuntime
            ?? throw new ArgumentNullException(nameof(equipmentMaintenanceRuntime));
        this.combatEquipmentRuntime = combatEquipmentRuntime
            ?? throw new ArgumentNullException(nameof(combatEquipmentRuntime));
        this.combatEquipmentCatalog = combatEquipmentCatalog
            ?? throw new ArgumentNullException(nameof(combatEquipmentCatalog));
        this.workOrderRuntime = workOrderRuntime
            ?? throw new ArgumentNullException(nameof(workOrderRuntime));
        this.doorAccessPanelPresenter = doorAccessPanelPresenter
            ?? throw new ArgumentNullException(nameof(doorAccessPanelPresenter));
        this.circusBuildingPanelPresenter = circusBuildingPanelPresenter
            ?? throw new ArgumentNullException(nameof(circusBuildingPanelPresenter));
        this.equipmentCraftingPanelPresenter = equipmentCraftingPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(equipmentCraftingPanelPresenter));
        this.instanceEvolutionPanelPresenter = instanceEvolutionPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(instanceEvolutionPanelPresenter));
        this.productionBuildingPanelPresenter = productionBuildingPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(productionBuildingPanelPresenter));
        this.cropPlotBuildingPanelPresenter = cropPlotBuildingPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(cropPlotBuildingPanelPresenter));
        this.animalHusbandryPanelPresenter = animalHusbandryPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(animalHusbandryPanelPresenter));
    }

    [Inject]
    public void ConstructUIBuildingInfoEventBus(IGameEventBus gameEventBus)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToInfoFeed();
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Start()
    {
        EnsureInitialized();
        if (hidden)
        {
            SetHiddenImmediate();
        }
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (buildingImageObject != null)
        {
            buildingImage = buildingImageObject.GetComponent<Image>();
            buildingImageSize = buildingImageObject.GetComponent<RectTransform>();
        }

        initialized = true;
    }

    public void DisplayBuildingInfo(BuildableObject building)
    {
        EnsureInitialized();

        if (building == null)
        {
            throw new ArgumentNullException(nameof(building));
        }

        if (building != selectedBuilding && !hidden) return;
        selectedBuilding = building;
        BuildingSO buildingData = ResolveBuildingLookup().GetBuilding(building.id) ?? building.BuildingData;
        if (buildingData == null)
        {
            return;
        }

        ResolvePopupService().CloseAll();
        OpenDispaly();
        if (buildingImageObject != null)
        {
            Vector2 size = new Vector2(Mathf.Max(40f, (buildingData.width / 3f) * 160f), 160f);
            if (buildingImageSize != null)
            {
                buildingImageSize.sizeDelta = size;
            }
            if (buildingImage != null)
            {
                ApplyBuildingPreview(buildingData.icon);
            }
        }
        if (nameText != null)
        {
            nameText.text = buildingData.objectName;
        }

        BuildingSummaryPresentation presentation = ResolveSummaryFormatter().Format(building);
        IReadOnlyList<string> details = presentation.DetailLines;
        List<UIConfig<TMP_Text>> detailViews = simpleInfoText ?? new List<UIConfig<TMP_Text>>();
        for (int index = 0; index < detailViews.Count; index++)
        {
            UIConfig<TMP_Text> ui = detailViews[index];
            if (ui?.uiObject == null) continue;

            bool visible = index < details.Count;
            ui.uiObject.gameObject.SetActive(visible);
            if (visible)
            {
                ui.uiObject.text = details[index];
                ui.uiObject.color = DungeonUiTheme.TextPrimary;
                ui.uiObject.fontSize = 24f;
                ui.uiObject.enableAutoSizing = true;
                ui.uiObject.fontSizeMin = 14f;
                ui.uiObject.fontSizeMax = 24f;
            }
        }

        RenderContextActions(buildingData, building);
    }

    private void ApplyBuildingPreview(Sprite sprite)
    {
        buildingImage.sprite = sprite;
        buildingImage.color = Color.white;
        buildingImage.material = null;
        buildingImage.type = Image.Type.Simple;
        buildingImage.preserveAspect = true;
        buildingImage.raycastTarget = false;
    }

    public void OpenDispaly()
    {
        EnsureInitialized();
        hidden = false;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        ResolvePopupService().BlockTouch();
        canvasGroup.DOKill();
        canvasGroup.DOFade(1.0f, 0.1f).SetUpdate(true);
    }
    public void CloseDispaly()
    {
        EnsureInitialized();
        hidden = true;
        selectedBuilding = null;
        ClearCraftActions();

        if (!gameObject.activeInHierarchy)
        {
            SetHiddenImmediate();
            ResolvePopupService().ReleaseTouch();
            return;
        }

        canvasGroup.DOKill();
        canvasGroup.DOFade(0f, 0.1f).SetUpdate(true).OnComplete(() =>
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            ResolvePopupService().ReleaseTouch();
        });
    }

    public void OnTriggerEvent(InfoFeedEvent eventType)
    {
        if (eventType.Target is not CharacterActor actor || actor == null)
        {
            return;
        }

        CloseDisplayImmediate();
    }

    private void OnEnable()
    {
        SubscribeToInfoFeed();
    }

    private void OnDisable()
    {
        infoFeedSubscription?.Dispose();
        infoFeedSubscription = null;
    }

    private void SubscribeToInfoFeed()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        infoFeedSubscription ??=
            gameEventBus.Subscribe<InfoFeedEvent>(OnTriggerEvent);
    }

    private void CloseDisplayImmediate()
    {
        EnsureInitialized();
        hidden = true;
        selectedBuilding = null;
        ClearCraftActions();
        SetHiddenImmediate();
        ResolvePopupService().ReleaseTouch();
        gameObject.SetActive(false);
    }

    private void RenderContextActions(BuildingSO buildingData, BuildableObject building)
    {
        ClearCraftActions();
        if (building is ConstructionSite constructionSite)
        {
            RenderConstructionActions(constructionSite);
            return;
        }

        IReadOnlyList<GameObject> equipmentCraftingObjects =
            equipmentCraftingPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(equipmentCraftingObjects);
        IReadOnlyList<GameObject> evolutionObjects =
            instanceEvolutionPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(evolutionObjects);
        RenderMaintenanceActions(buildingData, building);
        IReadOnlyList<GameObject> productionObjects =
            productionBuildingPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(productionObjects);
        IReadOnlyList<GameObject> cropObjects =
            cropPlotBuildingPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(cropObjects);
        IReadOnlyList<GameObject> husbandryObjects =
            animalHusbandryPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(husbandryObjects);
        if (building is Door door)
        {
            IReadOnlyList<GameObject> doorObjects = doorAccessPanelPresenter.Render(
                RequireContextActionsRoot(),
                door,
                nameText != null ? nameText.font : null,
                () => DisplayBuildingInfo(door));
            craftActionObjects.AddRange(doorObjects);
        }
        if (buildingData.GetCircusStageAbility() != null)
        {
            IReadOnlyList<GameObject> circusObjects =
                circusBuildingPanelPresenter.Render(
                    RequireContextActionsRoot(),
                    building,
                    nameText != null ? nameText.font : null,
                    () => DisplayBuildingInfo(building));
            craftActionObjects.AddRange(circusObjects);
        }
    }

    private void RenderConstructionActions(ConstructionSite site)
    {
        craftStatusMessage = string.Empty;
        if (site == null)
        {
            return;
        }

        Transform actionsRoot = RequireContextActionsRoot();
        if (workOrderRuntime.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out WorkOrderProgressState order))
        {
            craftActionObjects.Add(CreateConstructionProgressBar(actionsRoot, order));
        }

        GameObject cancelButton = CreateCraftButton(
            actionsRoot,
            "공사 취소",
            () =>
            {
                site.CancelConstruction();
                CloseDispaly();
            });
        cancelButton.name = "BuildingConstructionCancel";
        craftActionObjects.Add(cancelButton);
    }

    private void RenderMaintenanceActions(
        BuildingSO buildingData,
        BuildableObject building)
    {
        if (buildingData?.GetAbility<BuildingEquipmentMaintenanceAbility>() == null
            || building == null)
        {
            return;
        }

        IReadOnlyList<CombatEquipmentRepairOrder> orders =
            equipmentMaintenanceRuntime.Orders
                .Where(order =>
                    order != null
                    && order.FacilityPosition == building.centerPos)
                .OrderBy(order => order.orderId, StringComparer.Ordinal)
                .ToArray();
        Transform actionsRoot = RequireContextActionsRoot();
        GameObject header = CreateCraftStatus(
            actionsRoot,
            orders.Count == 0
                ? "장비 수리 대기열이 비어 있습니다."
                : $"장비 수리 대기열 {orders.Count}건");
        header.name = "BuildingMaintenanceHeader";
        craftActionObjects.Add(header);

        ICombatEquipmentCatalog catalog = combatEquipmentCatalog;
        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        for (int i = 0; i < orders.Count; i++)
        {
            CombatEquipmentRepairOrder order = orders[i];
            string equipmentName = order.equipmentInstanceId;
            if (equipment != null
                && equipment.TryGetInstance(
                    order.equipmentInstanceId,
                    out CombatEquipmentInstance instance)
                && catalog.TryGet(
                    instance.definitionId,
                    out CombatEquipmentDefinitionSO definition))
            {
                equipmentName = definition.DisplayName;
            }

            GameObject progress = CreateMaintenanceProgressBar(
                actionsRoot,
                equipmentName,
                order);
            progress.name = $"BuildingMaintenance_{i}";
            craftActionObjects.Add(progress);
        }
    }

    private GameObject CreateCraftButton(Transform parent, string label, Action callback)
    {
        GameObject buttonObject = new GameObject("BuildingCraftButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = DungeonUiTheme.Accent;

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 180f;
        layout.preferredHeight = 46f;

        Button button = buttonObject.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, selected: true);
        button.onClick.AddListener(() => callback?.Invoke());

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = label;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 13f;
        text.fontSizeMax = 20f;
        text.textWrappingMode = TextWrappingModes.Normal;
        if (nameText != null && nameText.font != null)
        {
            text.font = nameText.font;
        }

        return buttonObject;
    }

    private GameObject CreateConstructionProgressBar(Transform parent, WorkOrderProgressState order)
    {
        GameObject barObject = new GameObject("BuildingConstructionProgress", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        barObject.transform.SetParent(parent, false);
        LayoutElement layout = barObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 360f;
        layout.preferredHeight = 38f;

        Image background = barObject.GetComponent<Image>();
        background.color = DungeonUiTheme.Panel;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(barObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(order?.ProgressRatio ?? 0f), 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.GetComponent<Image>();
        fill.color = DungeonUiTheme.Accent;
        fill.raycastTarget = false;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(barObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        int percent = Mathf.RoundToInt((order?.ProgressRatio ?? 0f) * 100f);
        text.text = $"공사 진행 {percent}%";
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 18f;
        text.raycastTarget = false;
        if (nameText != null && nameText.font != null)
        {
            text.font = nameText.font;
        }

        return barObject;
    }

    private GameObject CreateMaintenanceProgressBar(
        Transform parent,
        string equipmentName,
        CombatEquipmentRepairOrder order)
    {
        GameObject barObject = new GameObject(
            "BuildingMaintenanceProgress",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        barObject.transform.SetParent(parent, false);
        LayoutElement layout = barObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 360f;
        layout.preferredHeight = 48f;
        barObject.GetComponent<Image>().color = DungeonUiTheme.Panel;

        GameObject fillObject = new GameObject(
            "Fill",
            typeof(RectTransform),
            typeof(Image));
        fillObject.transform.SetParent(barObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(order.ProgressRatio, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.GetComponent<Image>();
        fill.color = DungeonUiTheme.Accent;
        fill.raycastTarget = false;

        GameObject textObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(barObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = $"{equipmentName} · {FormatRepairState(order.state)}"
            + $" · {order.ProgressRatio:P0}"
            + $" · 재료 {order.requiredGeneralMaterials}";
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 11f;
        text.fontSizeMax = 17f;
        text.raycastTarget = false;
        if (nameText != null && nameText.font != null)
        {
            text.font = nameText.font;
        }

        return barObject;
    }

    private GameObject CreateCraftStatus(Transform parent, string message)
    {
        GameObject statusObject = new GameObject("BuildingCraftStatus", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        statusObject.transform.SetParent(parent, false);
        LayoutElement layout = statusObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 360f;
        layout.preferredHeight = 46f;

        TMP_Text text = statusObject.GetComponent<TMP_Text>();
        text.text = message;
        text.color = DungeonUiTheme.TextSecondary;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 18f;
        text.textWrappingMode = TextWrappingModes.Normal;
        if (nameText != null && nameText.font != null)
        {
            text.font = nameText.font;
        }

        return statusObject;
    }

    private void ClearCraftActions()
    {
        foreach (GameObject item in craftActionObjects)
        {
            if (item == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                item.SetActive(false);
                Destroy(item);
            }
            else
            {
                DestroyImmediate(item);
            }
        }

        craftActionObjects.Clear();
        if (contextActionsPanel != null)
        {
            contextActionsPanel.SetActive(false);
        }
    }

    private Transform RequireContextActionsRoot()
    {
        EnsureContextActionsPanel();
        contextActionsPanel.SetActive(true);
        contextActionsPanel.transform.SetAsLastSibling();
        return contextActionsContent;
    }

    private void EnsureContextActionsPanel()
    {
        if (contextActionsPanel != null && contextActionsContent != null)
        {
            return;
        }

        Transform existing = transform.Find("BuildingContextActions");
        if (existing != null)
        {
            contextActionsPanel = existing.gameObject;
            contextActionsContent = existing
                .Find("Viewport/Content") as RectTransform;
            if (contextActionsContent != null)
            {
                return;
            }

            Destroy(existing.gameObject);
        }

        contextActionsPanel = new GameObject(
            "BuildingContextActions",
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));
        contextActionsPanel.transform.SetParent(transform, false);
        RectTransform panelRect = contextActionsPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.38f, 0.08f);
        panelRect.anchorMax = new Vector2(0.98f, 0.72f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        contextActionsPanel.GetComponent<Image>().color = DungeonUiTheme.Surface;

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));
        viewportObject.transform.SetParent(contextActionsPanel.transform, false);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(8f, 8f);
        viewport.offsetMax = new Vector2(-24f, -8f);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);

        GameObject contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        contextActionsContent = contentObject.GetComponent<RectTransform>();
        contextActionsContent.anchorMin = new Vector2(0f, 1f);
        contextActionsContent.anchorMax = new Vector2(1f, 1f);
        contextActionsContent.pivot = new Vector2(0.5f, 1f);
        contextActionsContent.anchoredPosition = Vector2.zero;
        contextActionsContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = CreateContextScrollbar(contextActionsPanel.transform);
        ScrollRect scroll = contextActionsPanel.GetComponent<ScrollRect>();
        scroll.content = contextActionsContent;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility =
            ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = 4f;
        contextActionsPanel.SetActive(false);
    }

    private static Scrollbar CreateContextScrollbar(Transform parent)
    {
        GameObject root = new GameObject(
            "Scrollbar",
            typeof(RectTransform),
            typeof(Image),
            typeof(Scrollbar));
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = Vector2.one;
        rootRect.offsetMin = new Vector2(-16f, 8f);
        rootRect.offsetMax = new Vector2(-4f, -8f);
        root.GetComponent<Image>().color = DungeonUiTheme.SurfaceMuted;

        GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
        slidingArea.transform.SetParent(root.transform, false);
        RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.offsetMin = new Vector2(2f, 2f);
        slidingRect.offsetMax = new Vector2(-2f, -2f);

        GameObject handleObject = new GameObject(
            "Handle",
            typeof(RectTransform),
            typeof(Image));
        handleObject.transform.SetParent(slidingArea.transform, false);
        RectTransform handle = handleObject.GetComponent<RectTransform>();
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        handle.offsetMin = Vector2.zero;
        handle.offsetMax = Vector2.zero;
        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = DungeonUiTheme.Accent;

        Scrollbar scrollbar = root.GetComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }

    private static string FormatCraftMessage(string message)
    {
        return message switch
        {
            "craft-cost-not-available" => "재료 부족",
            "craft-cost-withdraw-failed" => "재료 출고 실패",
            "equipment-not-found" => "장비 정의 없음",
            _ => string.IsNullOrWhiteSpace(message) ? "제작 실패" : message
        };
    }

    private static string FormatRepairState(CombatEquipmentRepairOrderState state)
    {
        return state switch
        {
            CombatEquipmentRepairOrderState.PendingCombatEnd => "교전 종료 대기",
            CombatEquipmentRepairOrderState.WaitingForDelivery => "운반 대기",
            CombatEquipmentRepairOrderState.Ready => "수리 준비",
            CombatEquipmentRepairOrderState.InProgress => "수리 중",
            _ => "대기"
        };
    }

    private static string SanitizeObjectName(string value)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        char[] chars = normalized
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray();
        return new string(chars);
    }

    private void SetHiddenImmediate()
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.DOKill();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private IBuildingDefinitionLookup ResolveBuildingLookup()
    {
        return buildingDefinitionLookup
            ?? throw new InvalidOperationException($"{nameof(UIBuildingInfo)} requires {nameof(IBuildingDefinitionLookup)} injection.");
    }

    private IUiPopupService ResolvePopupService()
    {
        return popupService
            ?? throw new InvalidOperationException($"{nameof(UIBuildingInfo)} requires {nameof(IUiPopupService)} injection.");
    }

    private IBuildingSummaryFormatter ResolveSummaryFormatter()
    {
        return summaryFormatter
            ?? throw new InvalidOperationException($"{nameof(UIBuildingInfo)} requires {nameof(IBuildingSummaryFormatter)} injection.");
    }
}
[Serializable]
public class UIConfig<T>
{
    public string name;
    public T uiObject;
}
