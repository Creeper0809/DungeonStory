using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public interface ICodexPanelFactory
{
    CodexPanel CreateDefaultPanel(CodexRuntime runtime);
}

public interface IFacilitySynthesisPanelFactory
{
    FacilitySynthesisPanel CreateDefaultPanel(FacilitySynthesisRuntime runtime);
}

public interface IFacilityEvolutionPanelFactory
{
    FacilityEvolutionPanel CreateDefaultPanel(FacilityEvolutionRuntime runtime);
}

public sealed class CodexPanelFactory : ICodexPanelFactory
{
    private readonly ITmpKoreanFontService tmpKoreanFontService;
    private readonly IObjectResolver objectResolver;

    public CodexPanelFactory(ITmpKoreanFontService tmpKoreanFontService)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
    }

    [Inject]
    public CodexPanelFactory(
        ITmpKoreanFontService tmpKoreanFontService,
        IObjectResolver objectResolver)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
    }

    public CodexPanel CreateDefaultPanel(CodexRuntime runtime)
    {
        GameObject canvasObject = RuntimePanelFactoryUtility.CreateOverlayCanvas("CodexCanvas", new Vector2(1920f, 1080f));
        GameObject panelObject = RuntimePanelFactoryUtility.CreatePanel(
            canvasObject.transform,
            "CodexPanel",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-24f, 0f),
            new Vector2(520f, 680f));
        TMP_Text text = CreateSummaryText(panelObject.transform, 20f, new Vector2(20f, 20f), new Vector2(-20f, -20f));

        CodexPanel panel = panelObject.AddComponent<CodexPanel>();
        objectResolver?.Inject(panel);
        panel.BindGeneratedView(text);
        panel.Bind(runtime);
        panel.Refresh();
        return panel;
    }

    private TMP_Text CreateSummaryText(Transform parent, float fontSize, Vector2 offsetMin, Vector2 offsetMax)
    {
        return RuntimePanelFactoryUtility.CreateSummaryText(parent, tmpKoreanFontService, fontSize, offsetMin, offsetMax);
    }
}

public sealed class FacilitySynthesisPanelFactory : IFacilitySynthesisPanelFactory
{
    private readonly ITmpKoreanFontService tmpKoreanFontService;
    private readonly IObjectResolver objectResolver;

    public FacilitySynthesisPanelFactory(ITmpKoreanFontService tmpKoreanFontService)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
    }

    [Inject]
    public FacilitySynthesisPanelFactory(
        ITmpKoreanFontService tmpKoreanFontService,
        IObjectResolver objectResolver)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
    }

    public FacilitySynthesisPanel CreateDefaultPanel(FacilitySynthesisRuntime runtime)
    {
        GameObject canvasObject = RuntimePanelFactoryUtility.CreateOverlayCanvas("FacilitySynthesisCanvas", new Vector2(1920f, 1080f));
        GameObject panelObject = RuntimePanelFactoryUtility.CreatePanel(
            canvasObject.transform,
            "FacilitySynthesisPanel",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-24f, 0f),
            new Vector2(420f, 520f));
        TMP_Text text = RuntimePanelFactoryUtility.CreateSummaryText(
            panelObject.transform,
            tmpKoreanFontService,
            22f,
            new Vector2(18f, 18f),
            new Vector2(-18f, -18f));

        FacilitySynthesisPanel panel = panelObject.AddComponent<FacilitySynthesisPanel>();
        objectResolver?.Inject(panel);
        panel.BindGeneratedView(text);
        panel.Bind(runtime);
        panel.Refresh();
        return panel;
    }
}

public sealed class FacilityEvolutionPanelFactory : IFacilityEvolutionPanelFactory
{
    private readonly ITmpKoreanFontService tmpKoreanFontService;
    private readonly IObjectResolver objectResolver;

    public FacilityEvolutionPanelFactory(ITmpKoreanFontService tmpKoreanFontService)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
    }

    [Inject]
    public FacilityEvolutionPanelFactory(
        ITmpKoreanFontService tmpKoreanFontService,
        IObjectResolver objectResolver)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
    }

    public FacilityEvolutionPanel CreateDefaultPanel(FacilityEvolutionRuntime runtime)
    {
        GameObject canvasObject = RuntimePanelFactoryUtility.CreateOverlayCanvas("FacilityEvolutionCanvas", new Vector2(1920f, 1080f));
        GameObject panelObject = RuntimePanelFactoryUtility.CreatePanel(
            canvasObject.transform,
            "FacilityEvolutionPanel",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-468f, 0f),
            new Vector2(420f, 560f));
        TMP_Text text = RuntimePanelFactoryUtility.CreateSummaryText(
            panelObject.transform,
            tmpKoreanFontService,
            21f,
            new Vector2(18f, 18f),
            new Vector2(-18f, -18f));

        FacilityEvolutionPanel panel = panelObject.AddComponent<FacilityEvolutionPanel>();
        objectResolver?.Inject(panel);
        panel.BindGeneratedView(text);
        panel.Bind(runtime);
        panel.Refresh();
        return panel;
    }
}

internal static class RuntimePanelFactoryUtility
{
    public static GameObject CreateOverlayCanvas(string name, Vector2 referenceResolution)
    {
        GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        return canvasObject;
    }

    public static GameObject CreatePanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        panelObject.GetComponent<Image>().color = DungeonUiTheme.Panel;
        return panelObject;
    }

    public static TMP_Text CreateSummaryText(
        Transform parent,
        ITmpKoreanFontService tmpKoreanFontService,
        float fontSize,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject textObject = new GameObject("Summary", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = offsetMin;
        textRect.offsetMax = offsetMax;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        tmpKoreanFontService.Apply(text);
        text.fontSize = fontSize;
        text.color = DungeonUiTheme.TextPrimary;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = TextAlignmentOptions.TopLeft;
        return text;
    }
}
