using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public interface IP0FeatureSurfacePanelFactory
{
    P0FeatureSurfacePanel Ensure(GameObject panelObject, TabId tabId);
}

public sealed class P0FeatureSurfacePanelFactory : IP0FeatureSurfacePanelFactory
{
    private readonly IObjectResolver objectResolver;

    public P0FeatureSurfacePanelFactory(IObjectResolver objectResolver)
    {
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
    }

    public P0FeatureSurfacePanel Ensure(GameObject panelObject, TabId tabId)
    {
        if (panelObject == null)
        {
            throw new ArgumentNullException(nameof(panelObject));
        }

        P0FeatureSurfacePanel panel = panelObject.GetComponent<P0FeatureSurfacePanel>();
        if (panel == null)
        {
            panel = panelObject.AddComponent<P0FeatureSurfacePanel>();
        }

        panel.SetTabId(tabId);
        objectResolver.Inject(panel);
        return panel;
    }
}

public sealed partial class P0FeatureSurfacePanel : MonoBehaviour, IFeatureSurfaceView
{
    private const float SectionSpacing = 10f;
    private const float CardHeight = 86f;
    private const float CompactCardHeight = 66f;
    private const float EventAlertSafeRightInset = EventAlertLayout.AlertButtonWidth + 32f;
    private const int MaxVisibleCardsPerSection = 8;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();

    private TabId? tabId;
    private RectTransform contentRoot;
    private TMP_Text feedbackText;
    private string feedbackMessage = "작업 대기";
    private bool layoutReady;

    private ITmpKoreanFontService fontService;
    private IFeatureSurfaceTabPresenterRegistry presenterRegistry;

    [Inject]
    public void Construct(
        ITmpKoreanFontService fontService,
        IFeatureSurfaceTabPresenterRegistry presenterRegistry)
    {
        this.fontService = fontService
            ?? throw new ArgumentNullException(nameof(fontService));
        this.presenterRegistry = presenterRegistry
            ?? throw new ArgumentNullException(nameof(presenterRegistry));
    }

    public void SetTabId(TabId id)
    {
        tabId = id;
    }

    private void Awake()
    {
        UITab tab = GetComponent<UITab>();
        UITabIdentity identity = GetComponent<UITabIdentity>();
        if (!tabId.HasValue && identity != null)
        {
            tabId = identity.Id;
        }
        else if (!tabId.HasValue && tab != null && UITabCatalog.TryFromLegacyId(tab.id, out TabId legacyId))
        {
            tabId = legacyId;
        }
    }

    private void Start()
    {
        Refresh();
    }

    private void OnEnable()
    {
        if (layoutReady)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        EnsureLayout();
        Rebuild();
    }

    private void EnsureLayout()
    {
        if (layoutReady && contentRoot != null)
        {
            return;
        }

        RectTransform host = ResolveBodyHost();
        ClearChildren(host);

        GameObject scrollObject = CreateUiObject("P0SurfaceScroll", host);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = Vector2.zero;

        Image scrollBackground = scrollObject.AddComponent<Image>();
        scrollBackground.color = DungeonUiTheme.SurfaceMuted;

        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 18f;

        GameObject viewportObject = CreateUiObject("Viewport", scrollRectTransform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(12f, 12f);
        viewportRect.offsetMax = new Vector2(-EventAlertSafeRightInset, -12f);

        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        Mask mask = viewportObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentObject = CreateUiObject("Content", viewportRect);
        contentRoot = contentObject.GetComponent<RectTransform>();
        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0.5f, 1f);
        contentRoot.anchoredPosition = Vector2.zero;
        contentRoot.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vertical = contentObject.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = SectionSpacing;
        vertical.padding = new RectOffset(0, 0, 0, 14);
        vertical.childControlWidth = true;
        vertical.childControlHeight = false;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRoot;
        layoutReady = true;
    }

    private RectTransform ResolveBodyHost()
    {
        Transform body = transform.Find("Body");
        RectTransform host = body as RectTransform;
        TMP_Text placeholder = body != null ? body.GetComponent<TMP_Text>() : null;
        if (placeholder != null)
        {
            placeholder.text = string.Empty;
            placeholder.enabled = false;
        }

        if (host != null)
        {
            return host;
        }

        return GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
    }

    private void Rebuild()
    {
        ClearSpawnedObjects();
        feedbackText = null;

        AddFeedback();

        if (!tabId.HasValue)
        {
            AddEmptyState("기능 패널의 안정 ID가 설정되지 않았습니다.");
            return;
        }

        if (presenterRegistry == null)
        {
            throw new InvalidOperationException(
                $"{nameof(P0FeatureSurfacePanel)} requires {nameof(IFeatureSurfaceTabPresenterRegistry)} injection.");
        }

        if (!presenterRegistry.TryGet(tabId.Value, out IFeatureSurfaceTabPresenter presenter))
        {
            AddEmptyState($"기능 presenter가 등록되지 않은 탭입니다. id={tabId.Value}");
            return;
        }

        presenter.Present(this);
    }


    private void CreateDataCard(
        string actionName,
        string title,
        string detail,
        string buttonText,
        Action onClick,
        float height)
    {
        GameObject card = CreateUiObject(actionName + "_Card", contentRoot);
        spawnedObjects.Add(card);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, height);

        Image image = card.AddComponent<Image>();
        image.color = DungeonUiTheme.Surface;

        HorizontalLayoutGroup horizontal = card.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 10f;
        horizontal.padding = new RectOffset(12, 12, 8, 8);
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;

        LayoutElement cardLayout = card.AddComponent<LayoutElement>();
        cardLayout.preferredHeight = height;
        cardLayout.minHeight = height;

        GameObject textColumn = CreateUiObject("Text", card.transform);
        VerticalLayoutGroup textVertical = textColumn.AddComponent<VerticalLayoutGroup>();
        textVertical.spacing = 2f;
        textVertical.childControlWidth = true;
        textVertical.childControlHeight = false;
        textVertical.childForceExpandWidth = true;
        textVertical.childForceExpandHeight = false;
        LayoutElement textLayout = textColumn.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;

        TMP_Text titleText = AddText(textColumn.transform, title, 20f, FontStyles.Bold);
        titleText.color = DungeonUiTheme.TextPrimary;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 14f;
        titleText.fontSizeMax = 20f;
        titleText.overflowMode = TextOverflowModes.Truncate;

        TMP_Text detailText = AddText(textColumn.transform, detail, 16f, FontStyles.Normal);
        detailText.color = DungeonUiTheme.TextSecondary;
        detailText.enableAutoSizing = true;
        detailText.fontSizeMin = 11f;
        detailText.fontSizeMax = 16f;
        detailText.textWrappingMode = TextWrappingModes.Normal;

        CreateActionButton(card.transform, actionName, buttonText, onClick, 132f, height - 18f);
    }

    private void CreateStatusCard(string stateName, string title, string detail, float height)
    {
        GameObject card = CreateUiObject(stateName, contentRoot);
        spawnedObjects.Add(card);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, height);

        Image image = card.AddComponent<Image>();
        image.color = DungeonUiTheme.Surface;
        image.raycastTarget = false;

        VerticalLayoutGroup vertical = card.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 2f;
        vertical.padding = new RectOffset(12, 12, 8, 8);
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        LayoutElement cardLayout = card.AddComponent<LayoutElement>();
        cardLayout.preferredHeight = height;
        cardLayout.minHeight = height;

        TMP_Text titleText = AddText(card.transform, title, 20f, FontStyles.Bold);
        titleText.color = DungeonUiTheme.TextPrimary;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 14f;
        titleText.fontSizeMax = 20f;
        titleText.overflowMode = TextOverflowModes.Truncate;
        titleText.raycastTarget = false;
        LayoutElement titleLayout = titleText.gameObject.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 22f;
        titleLayout.minHeight = 20f;

        TMP_Text detailText = AddText(card.transform, detail, 16f, FontStyles.Normal);
        detailText.color = DungeonUiTheme.TextSecondary;
        detailText.enableAutoSizing = true;
        detailText.fontSizeMin = 11f;
        detailText.fontSizeMax = 16f;
        detailText.textWrappingMode = TextWrappingModes.Normal;
        detailText.raycastTarget = false;
        LayoutElement detailLayout = detailText.gameObject.AddComponent<LayoutElement>();
        detailLayout.preferredHeight = Mathf.Max(20f, height - 40f);
        detailLayout.minHeight = 18f;
    }

    private void CreateButtonRow(string actionName, string buttonText, string detail, Action onClick)
    {
        CreateDataCard(actionName, buttonText, detail, buttonText, onClick, CompactCardHeight);
    }

    private void AddSection(string title, string summary)
    {
        GameObject section = CreateUiObject("Section_" + title, contentRoot);
        spawnedObjects.Add(section);
        RectTransform rect = section.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 58f);

        Image image = section.AddComponent<Image>();
        image.color = DungeonUiTheme.SurfaceRaised;

        VerticalLayoutGroup vertical = section.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 1f;
        vertical.padding = new RectOffset(12, 12, 7, 5);
        vertical.childControlWidth = true;
        vertical.childControlHeight = false;

        LayoutElement layout = section.AddComponent<LayoutElement>();
        layout.preferredHeight = 58f;
        layout.minHeight = 58f;

        TMP_Text titleText = AddText(section.transform, title, 21f, FontStyles.Bold);
        titleText.color = DungeonUiTheme.Warning;
        TMP_Text summaryText = AddText(section.transform, summary, 15f, FontStyles.Normal);
        summaryText.color = DungeonUiTheme.TextSecondary;
        summaryText.overflowMode = TextOverflowModes.Truncate;
    }

    private void AddFeedback()
    {
        GameObject feedback = CreateUiObject("P0Feedback", contentRoot);
        spawnedObjects.Add(feedback);
        RectTransform rect = feedback.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 46f);

        Image image = feedback.AddComponent<Image>();
        image.color = DungeonUiTheme.SurfaceRaised;

        LayoutElement layout = feedback.AddComponent<LayoutElement>();
        layout.preferredHeight = 46f;
        layout.minHeight = 46f;

        feedbackText = AddText(feedback.transform, feedbackMessage, 18f, FontStyles.Bold);
        feedbackText.color = DungeonUiTheme.Warning;
        RectTransform textRect = feedbackText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 3f);
        textRect.offsetMax = new Vector2(-12f, -3f);
        feedbackText.alignment = TextAlignmentOptions.MidlineLeft;
        feedbackText.enableAutoSizing = true;
        feedbackText.fontSizeMin = 11f;
        feedbackText.fontSizeMax = 18f;
        feedbackText.overflowMode = TextOverflowModes.Truncate;
    }

    private void AddLabel(string text, float fontSize, float height)
    {
        GameObject row = CreateUiObject("Label", contentRoot);
        spawnedObjects.Add(row);
        LayoutElement layout = row.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;

        TMP_Text label = AddText(row.transform, text, fontSize, FontStyles.Normal);
        label.color = DungeonUiTheme.TextPrimary;
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.Normal;
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);
    }

    private void AddEmptyState(string message)
    {
        AddSection("연결 상태", "필요한 런타임이 없어서 조작 UI를 만들 수 없습니다.");
        AddLabel(message, 20f, 64f);
    }

    private Button CreateActionButton(
        Transform parent,
        string actionName,
        string label,
        Action onClick,
        float width,
        float height)
    {
        GameObject buttonObject = CreateUiObject(actionName, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);

        Image image = buttonObject.AddComponent<Image>();
        image.color = DungeonUiTheme.Accent;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        DungeonUiTheme.StyleButton(button, selected: true);
        button.onClick.AddListener(() =>
        {
            onClick?.Invoke();
            Refresh();
        });

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = width;
        layout.preferredHeight = height;
        layout.minHeight = height;

        TMP_Text text = AddText(buttonObject.transform, label, 17f, FontStyles.Bold);
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 17f;
        text.textWrappingMode = TextWrappingModes.Normal;
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6f, 4f);
        textRect.offsetMax = new Vector2(-6f, -4f);
        text.raycastTarget = false;
        return button;
    }

    private TMP_Text AddText(Transform parent, string text, float fontSize, FontStyles style)
    {
        GameObject textObject = CreateUiObject("Text", parent);
        TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
        fontService?.Apply(label);
        label.text = text ?? string.Empty;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    private GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private void SetFeedback(string message)
    {
        feedbackMessage = string.IsNullOrWhiteSpace(message) ? "결과 없음" : message;
        if (feedbackText != null)
        {
            feedbackText.text = feedbackMessage;
        }
    }

    void IFeatureSurfaceView.AddSection(string title, string summary)
    {
        AddSection(title, summary);
    }

    void IFeatureSurfaceView.AddLabel(string text, float fontSize, float height)
    {
        AddLabel(text, fontSize, height);
    }

    void IFeatureSurfaceView.AddDataCard(
        string actionName,
        string title,
        string detail,
        string buttonText,
        Action onClick,
        float height)
    {
        CreateDataCard(actionName, title, detail, buttonText, onClick, height);
    }

    void IFeatureSurfaceView.ShowFeedback(string message)
    {
        SetFeedback(message);
    }

    void IFeatureSurfaceView.RequestRefresh()
    {
        Refresh();
    }

    private void ClearSpawnedObjects()
    {
        foreach (GameObject spawned in spawnedObjects)
        {
            Release(spawned);
        }

        spawnedObjects.Clear();
    }

    private void ClearChildren(RectTransform host)
    {
        if (host == null)
        {
            return;
        }

        for (int i = host.childCount - 1; i >= 0; i--)
        {
            Release(host.GetChild(i).gameObject);
        }
    }

    private static void Release(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        target.SetActive(false);
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
