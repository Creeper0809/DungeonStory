using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public readonly struct GeneratedUITabPanel
{
    public GeneratedUITabPanel(UITab tab, TMP_Text bodyText)
    {
        Tab = tab ?? throw new ArgumentNullException(nameof(tab));
        BodyText = bodyText ?? throw new ArgumentNullException(nameof(bodyText));
    }

    public UITab Tab { get; }
    public TMP_Text BodyText { get; }
}

public interface IUITabGeneratedPanelFactory
{
    GeneratedUITabPanel Create(Transform parent, TabId id, string panelTitle);
}

public interface IStaffWorkPriorityPanelFactory
{
    StaffWorkPriorityPanel Ensure(GameObject panelObject);
}

public sealed class UITabGeneratedPanelFactory : IUITabGeneratedPanelFactory
{
    private const float GeneratedPanelBottomOffset = 64f;
    private const float GeneratedPanelHeight = 520f;

    private readonly ITmpKoreanFontService tmpKoreanFontService;
    private readonly IObjectResolver objectResolver;
    private readonly IStaffWorkPriorityPanelFactory staffWorkPriorityPanelFactory;

    public UITabGeneratedPanelFactory(
        ITmpKoreanFontService tmpKoreanFontService,
        IObjectResolver objectResolver,
        IStaffWorkPriorityPanelFactory staffWorkPriorityPanelFactory)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
        this.staffWorkPriorityPanelFactory = staffWorkPriorityPanelFactory
            ?? throw new ArgumentNullException(nameof(staffWorkPriorityPanelFactory));
    }

    public GeneratedUITabPanel Create(Transform parent, TabId id, string panelTitle)
    {
        if (parent == null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        GameObject panelObject = new GameObject($"{panelTitle}Tab", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, GeneratedPanelBottomOffset);
        rect.sizeDelta = new Vector2(0f, GeneratedPanelHeight);

        Image background = panelObject.GetComponent<Image>();
        background.color = DungeonUiTheme.Panel;

        TMP_Text titleText = CreateText(panelObject.transform, "Title");
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleRect.sizeDelta = new Vector2(-48f, 42f);
        titleText.text = panelTitle;
        titleText.fontSize = 30f;
        titleText.color = DungeonUiTheme.TextPrimary;
        titleText.alignment = TextAlignmentOptions.Left;

        TMP_Text bodyText = CreateText(panelObject.transform, "Body");
        RectTransform bodyRect = bodyText.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(24f, 24f);
        bodyRect.offsetMax = new Vector2(-24f, -76f);
        bodyText.fontSize = 22f;
        bodyText.color = DungeonUiTheme.TextPrimary;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.textWrappingMode = TextWrappingModes.Normal;

        UITab tab = panelObject.AddComponent<UITab>();
        tab.id = (int)id;
        panelObject.AddComponent<UITabIdentity>().Set(id);
        objectResolver.Inject(tab);

        if (id == TabId.Staff)
        {
            staffWorkPriorityPanelFactory.Ensure(panelObject);
        }

        panelObject.SetActive(false);
        return new GeneratedUITabPanel(tab, bodyText);
    }

    private TMP_Text CreateText(Transform parent, string name)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        tmpKoreanFontService.Apply(text);
        return text;
    }
}

public sealed class StaffWorkPriorityPanelFactory : IStaffWorkPriorityPanelFactory
{
    private readonly IObjectResolver objectResolver;

    public StaffWorkPriorityPanelFactory(IObjectResolver objectResolver)
    {
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
    }

    public StaffWorkPriorityPanel Ensure(GameObject panelObject)
    {
        if (panelObject == null)
        {
            throw new ArgumentNullException(nameof(panelObject));
        }

        StaffWorkPriorityPanel panel = panelObject.GetComponent<StaffWorkPriorityPanel>();
        if (panel == null)
        {
            panel = panelObject.AddComponent<StaffWorkPriorityPanel>();
        }

        objectResolver.Inject(panel);
        DisablePlaceholderBody(panelObject);
        return panel;
    }

    private static void DisablePlaceholderBody(GameObject panelObject)
    {
        Transform body = panelObject.transform.Find("Body");
        TMP_Text bodyText = body != null ? body.GetComponent<TMP_Text>() : null;
        if (bodyText == null)
        {
            return;
        }

        bodyText.text = string.Empty;
        bodyText.enabled = false;
    }
}
