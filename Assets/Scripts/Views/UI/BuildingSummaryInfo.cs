using System;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class BuildingSummaryInfo : UIPopUp
{
    public GameObject UI;
    public TMP_Text objectName;
    public TMP_Text stock;

    private IUiPopupService popupService;
    private IBuildingSummaryFormatter summaryFormatter;
    private ITmpKoreanFontService tmpKoreanFontService;
    private Button contextActionButton;
    private TMP_Text contextActionLabel;
    private BuildableObject currentBuilding;
    private IGameEventBus gameEventBus;
    private IDisposable infoFeedSubscription;

    [Inject]
    public void Construct(
        IUiPopupService popupService,
        IBuildingSummaryFormatter summaryFormatter,
        ITmpKoreanFontService tmpKoreanFontService)
    {
        this.popupService = popupService
            ?? throw new ArgumentNullException(nameof(popupService));
        this.summaryFormatter = summaryFormatter
            ?? throw new ArgumentNullException(nameof(summaryFormatter));
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
    }

    [Inject]
    public void ConstructBuildingSummaryEventBus(IGameEventBus gameEventBus)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToInfoFeed();
    }

    private void Start()
    {
        GameObject uiRoot = RequireUiRoot();
        EnsureGeneratedView(uiRoot);
        RequireTmpKoreanFontService().ApplyToChildren(uiRoot.transform);
        uiRoot.SetActive(false);
    }

    public void OnTriggerEvent(InfoFeedEvent eventType)
    {
        if (eventType.Target is not BuildingInfoTarget buildingInfo || buildingInfo.Building == null) return;

        BuildableObject building = buildingInfo.Building;
        currentBuilding = building;
        GameObject uiRoot = RequireUiRoot();

        ResolvePopupService().CloseAll();
        EnsureGeneratedView(uiRoot);
        RequireTmpKoreanFontService().ApplyToChildren(uiRoot.transform);
        uiRoot.SetActive(true);
        ResolvePopupService().Open(this);

        BuildingSummaryPresentation presentation = ResolveSummaryFormatter().Format(building);
        RequireObjectNameText().text = presentation.ObjectName;
        RequireStockText().text = presentation.StockText;
        ConfigureContextAction(building);
    }

    private void EnsureGeneratedView(GameObject uiRoot)
    {
        const string viewName = "BuildingSummaryGeneratedView";
        ConfigurePanelBounds(uiRoot);

        Transform existing = uiRoot.transform.Find(viewName);
        if (existing != null)
        {
            objectName = existing.Find("Header/BuildingName")?.GetComponent<TMP_Text>();
            stock = existing.Find("Details")?.GetComponent<TMP_Text>();
            contextActionButton = existing.Find("CleanPriorityButton")?.GetComponent<Button>();
            contextActionLabel = contextActionButton != null
                ? contextActionButton.GetComponentInChildren<TMP_Text>(true)
                : null;
            return;
        }

        foreach (Transform child in uiRoot.transform)
        {
            child.gameObject.SetActive(false);
        }

        RectTransform view = CreateRect(viewName, uiRoot.transform);
        SetStretch(view, Vector2.zero, Vector2.zero);

        RectTransform header = CreateRect("Header", view);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = Vector2.zero;
        header.sizeDelta = new Vector2(0f, 66f);
        header.gameObject.AddComponent<Image>().color = DungeonUiTheme.SurfaceRaised;

        objectName = CreateText("BuildingName", header, 26f, FontStyles.Bold);
        objectName.color = DungeonUiTheme.TextPrimary;
        objectName.alignment = TextAlignmentOptions.MidlineLeft;
        objectName.enableAutoSizing = true;
        objectName.fontSizeMin = 17f;
        objectName.fontSizeMax = 26f;
        objectName.textWrappingMode = TextWrappingModes.NoWrap;
        objectName.overflowMode = TextOverflowModes.Truncate;
        SetStretch(objectName.rectTransform, new Vector2(18f, 6f), new Vector2(-92f, -6f));

        Button closeButton = CreateButton("CloseButton", header, "닫기");
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = Vector2.one;
        closeRect.anchorMax = Vector2.one;
        closeRect.pivot = Vector2.one;
        closeRect.anchoredPosition = new Vector2(-12f, -15f);
        closeRect.sizeDelta = new Vector2(68f, 36f);
        closeButton.onClick.AddListener(OnClose);

        stock = CreateText("Details", view, 18f, FontStyles.Normal);
        stock.color = DungeonUiTheme.TextSecondary;
        stock.alignment = TextAlignmentOptions.TopLeft;
        stock.textWrappingMode = TextWrappingModes.Normal;
        stock.overflowMode = TextOverflowModes.Truncate;
        stock.lineSpacing = 8f;
        stock.margin = new Vector4(14f, 14f, 14f, 14f);
        SetStretch(stock.rectTransform, new Vector2(14f, 14f), new Vector2(-14f, -78f));

        contextActionButton = CreateButton("CleanPriorityButton", view, "청소 우선");
        RectTransform actionRect = contextActionButton.GetComponent<RectTransform>();
        actionRect.anchorMin = new Vector2(1f, 0f);
        actionRect.anchorMax = new Vector2(1f, 0f);
        actionRect.pivot = new Vector2(1f, 0f);
        actionRect.anchoredPosition = new Vector2(-16f, 16f);
        actionRect.sizeDelta = new Vector2(146f, 42f);
        contextActionLabel = contextActionButton.GetComponentInChildren<TMP_Text>(true);
        contextActionButton.onClick.AddListener(OnContextAction);
        contextActionButton.gameObject.SetActive(false);
    }

    private void ConfigureContextAction(BuildableObject building)
    {
        bool show = building is WorldFilthWorkTarget;
        if (contextActionButton != null)
        {
            contextActionButton.gameObject.SetActive(show);
        }

        if (stock != null)
        {
            Vector2 offsetMin = stock.rectTransform.offsetMin;
            offsetMin.y = show ? 66f : 14f;
            stock.rectTransform.offsetMin = offsetMin;
        }

        if (show && building is WorldFilthWorkTarget filth && contextActionLabel != null)
        {
            contextActionLabel.text = filth.IsPriorityCleaning ? "우선 해제" : "청소 우선";
        }
    }

    private void OnContextAction()
    {
        if (currentBuilding is not WorldFilthWorkTarget filth || filth == null)
        {
            return;
        }

        filth.SetPriorityCleaning(!filth.IsPriorityCleaning);
        BuildingSummaryPresentation presentation = ResolveSummaryFormatter().Format(filth);
        RequireStockText().text = presentation.StockText;
        ConfigureContextAction(filth);
    }

    private TMP_Text CreateText(string name, Transform parent, float fontSize, FontStyles style)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        RequireTmpKoreanFontService().Apply(text);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.characterSpacing = 0f;
        text.raycastTarget = false;
        return text;
    }

    private TMP_Text CreateButtonLabel(Transform parent, string value)
    {
        TMP_Text label = CreateText("Label", parent, 15f, FontStyles.Bold);
        label.text = value;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        SetStretch(label.rectTransform, new Vector2(6f, 2f), new Vector2(-6f, -2f));
        return label;
    }

    private Button CreateButton(string name, Transform parent, string label)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        CreateButtonLabel(rect, label);
        DungeonUiTheme.StyleButton(button);
        return button;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static void ConfigurePanelBounds(GameObject uiRoot)
    {
        RectTransform wrapper = uiRoot.transform.parent as RectTransform;
        if (wrapper != null)
        {
            wrapper.anchorMin = Vector2.zero;
            wrapper.anchorMax = Vector2.zero;
            wrapper.pivot = Vector2.zero;
            wrapper.anchoredPosition = new Vector2(24f, 80f);
            wrapper.sizeDelta = new Vector2(460f, 360f);
        }

        RectTransform rootRect = uiRoot.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            SetStretch(rootRect, Vector2.zero, Vector2.zero);
        }

        Image background = uiRoot.GetComponent<Image>();
        if (background == null)
        {
            background = uiRoot.AddComponent<Image>();
        }

        background.color = DungeonUiTheme.Panel;
    }

    private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    public override void OnClose()
    {
        currentBuilding = null;
        RequireUiRoot().SetActive(false);
    }

    public void OnEnable()
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

    private IUiPopupService ResolvePopupService()
    {
        return popupService
            ?? throw new InvalidOperationException(
                $"{nameof(BuildingSummaryInfo)} requires VContainer injection of {nameof(IUiPopupService)}.");
    }

    private IBuildingSummaryFormatter ResolveSummaryFormatter()
    {
        return summaryFormatter
            ?? throw new InvalidOperationException(
                $"{nameof(BuildingSummaryInfo)} requires VContainer injection of {nameof(IBuildingSummaryFormatter)}.");
    }

    private ITmpKoreanFontService RequireTmpKoreanFontService()
    {
        return tmpKoreanFontService
            ?? throw new InvalidOperationException(
                $"{nameof(BuildingSummaryInfo)} requires VContainer injection of {nameof(ITmpKoreanFontService)}.");
    }

    private GameObject RequireUiRoot()
    {
        return UI
            ?? throw new InvalidOperationException($"{nameof(BuildingSummaryInfo)} requires a UI root reference.");
    }

    private TMP_Text RequireObjectNameText()
    {
        return objectName
            ?? throw new InvalidOperationException($"{nameof(BuildingSummaryInfo)} requires an object name text reference.");
    }

    private TMP_Text RequireStockText()
    {
        return stock
            ?? throw new InvalidOperationException($"{nameof(BuildingSummaryInfo)} requires a stock text reference.");
    }
}

public sealed class BuildingInfoTarget : IInfoable
{
    public BuildableObject Building { get; }

    public BuildingInfoTarget(BuildableObject building)
    {
        Building = building;
    }
}

public readonly struct InfoFeedEvent
{
    public InfoFeedEvent(IInfoable target)
    {
        Target = target;
    }

    public IInfoable Target { get; }
}

public static class GameEventBusInfoFeedExtensions
{
    public static void ShowInfo(this IGameEventBus gameEventBus, IInfoable target)
    {
        if (gameEventBus == null)
        {
            throw new ArgumentNullException(nameof(gameEventBus));
        }

        gameEventBus.Publish(new InfoFeedEvent(target));
    }
}
