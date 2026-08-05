using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using UnityEngine.Scripting.APIUpdating;

public interface IRunResultPanelFactory
{
    RunResultPanel CreateDefaultPanel();
}

public interface IRunResultThemeQuery
{
    Color ResultScrim { get; }
    Color Panel { get; }
    Color TextPrimary { get; }
    void StylePrimaryButton(Button button);
    void Apply(Canvas canvas);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class RunResultPanelFactory : IRunResultPanelFactory
{
    private readonly ITmpKoreanFontService tmpKoreanFontService;
    private readonly IObjectResolver objectResolver;
    private readonly IRunResultPanelRegistry panelRegistry;
    private readonly IRunResultThemeQuery theme;
    private readonly IRunResultTextQuery textQuery;

    public RunResultPanelFactory(
        ITmpKoreanFontService tmpKoreanFontService,
        IObjectResolver objectResolver,
        IRunResultPanelRegistry panelRegistry,
        IRunResultThemeQuery theme,
        IRunResultTextQuery textQuery)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new System.ArgumentNullException(nameof(tmpKoreanFontService));
        this.objectResolver = objectResolver
            ?? throw new System.ArgumentNullException(nameof(objectResolver));
        this.panelRegistry = panelRegistry
            ?? throw new System.ArgumentNullException(nameof(panelRegistry));
        this.theme = theme
            ?? throw new System.ArgumentNullException(nameof(theme));
        this.textQuery = textQuery
            ?? throw new System.ArgumentNullException(nameof(textQuery));
    }

    public RunResultPanel CreateDefaultPanel()
    {
        GameObject canvasObject = new GameObject("RunResultCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject scrimObject = new GameObject("RunResultInputBlocker", typeof(RectTransform), typeof(Image));
        scrimObject.transform.SetParent(canvasObject.transform, false);
        RectTransform scrimRect = scrimObject.GetComponent<RectTransform>();
        scrimRect.anchorMin = Vector2.zero;
        scrimRect.anchorMax = Vector2.one;
        scrimRect.offsetMin = Vector2.zero;
        scrimRect.offsetMax = Vector2.zero;
        scrimObject.GetComponent<Image>().color = theme.ResultScrim;

        GameObject panelObject = new GameObject("RunResultPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(700f, 640f);

        Image image = panelObject.GetComponent<Image>();
        image.color = theme.Panel;

        GameObject textObject = new GameObject("RunResultText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(32f, 88f);
        textRect.offsetMax = new Vector2(-32f, -24f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        tmpKoreanFontService.Apply(text);
        text.fontSize = 20f;
        text.color = theme.TextPrimary;
        text.alignment = TextAlignmentOptions.Top;
        text.textWrappingMode = TextWrappingModes.Normal;

        GameObject buttonObject = new GameObject("NextRunButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panelObject.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 24f);
        buttonRect.sizeDelta = new Vector2(190f, 56f);
        Button button = buttonObject.GetComponent<Button>();
        theme.StylePrimaryButton(button);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        tmpKoreanFontService.Apply(label);
        label.text = textQuery.Get(RunResultTextId.NextRun);
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = theme.TextPrimary;

        RunResultPanel panel = panelObject.AddComponent<RunResultPanel>();
        objectResolver.Inject(panel);
        panelRegistry.Register(panel);
        theme.Apply(canvas);
        return panel;
    }
}
