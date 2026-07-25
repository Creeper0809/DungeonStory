using TMPro;
using UnityEngine;
using VContainer;

public interface IOffensePanelFactory
{
    OffenseWorldMapPanel CreateWorldMapPanel();
    OffenseExpeditionPanel CreateExpeditionPanel();
}

public sealed class OffensePanelFactory : IOffensePanelFactory
{
    private readonly ITmpKoreanFontService tmpKoreanFontService;
    private readonly IObjectResolver objectResolver;
    private readonly OffenseSceneRuntimeReferences runtimeReferences;

    public OffensePanelFactory(
        ITmpKoreanFontService tmpKoreanFontService,
        IObjectResolver objectResolver,
        OffenseSceneRuntimeReferences runtimeReferences)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new System.ArgumentNullException(nameof(tmpKoreanFontService));
        this.objectResolver = objectResolver
            ?? throw new System.ArgumentNullException(nameof(objectResolver));
        this.runtimeReferences = runtimeReferences
            ?? throw new System.ArgumentNullException(nameof(runtimeReferences));
    }

    public OffenseWorldMapPanel CreateWorldMapPanel()
    {
        GameObject canvasObject = OffensePanelUiFactory.CreateOverlayCanvas(
            "OffenseWorldMapCanvas",
            420,
            new Vector2(1280f, 720f));
        GameObject panelObject = OffensePanelUiFactory.CreatePanel(
            canvasObject.transform,
            "OffenseWorldMapPanel",
            new Vector2(0.12f, 0.12f),
            new Vector2(0.88f, 0.88f),
            new Color(0.035f, 0.04f, 0.05f, 0.94f));

        GameObject header = OffensePanelUiFactory.CreateText(panelObject.transform, "OffenseWorldMapHeader", 25f, TextAlignmentOptions.Left, tmpKoreanFontService);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(24f, -66f);
        headerRect.offsetMax = new Vector2(-24f, -18f);

        GameObject buttonRootObject = OffensePanelUiFactory.CreateVerticalRoot(
            panelObject.transform,
            "OffenseWorldMapTargets",
            new Vector2(0f, 0f),
            new Vector2(0.36f, 0.86f),
            new Vector2(24f, 24f),
            new Vector2(-12f, -24f),
            8f);

        GameObject detail = OffensePanelUiFactory.CreateText(panelObject.transform, "OffenseWorldMapDetail", 20f, TextAlignmentOptions.TopLeft, tmpKoreanFontService);
        RectTransform detailRect = detail.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0.38f, 0f);
        detailRect.anchorMax = new Vector2(1f, 0.86f);
        detailRect.offsetMin = new Vector2(12f, 24f);
        detailRect.offsetMax = new Vector2(-24f, -24f);

        OffenseWorldMapPanel panel = panelObject.AddComponent<OffenseWorldMapPanel>();
        panel.BindGeneratedView(
            header.GetComponent<TMP_Text>(),
            detail.GetComponent<TMP_Text>(),
            buttonRootObject.GetComponent<RectTransform>());
        objectResolver.Inject(panel);
        runtimeReferences.RegisterWorldMapPanel(panel);
        return panel;
    }

    public OffenseExpeditionPanel CreateExpeditionPanel()
    {
        GameObject canvasObject = OffensePanelUiFactory.CreateOverlayCanvas(
            "OffenseExpeditionCanvas",
            430,
            new Vector2(1280f, 720f));
        GameObject panelObject = OffensePanelUiFactory.CreatePanel(
            canvasObject.transform,
            "OffenseExpeditionPanel",
            new Vector2(0.1f, 0.06f),
            new Vector2(0.9f, 0.94f),
            new Color(0.075f, 0.07f, 0.085f, 0.97f));

        GameObject header = OffensePanelUiFactory.CreateText(panelObject.transform, "OffenseExpeditionHeader", 24f, TextAlignmentOptions.Left, tmpKoreanFontService);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(24f, -64f);
        headerRect.offsetMax = new Vector2(-24f, -18f);

        GameObject memberRootObject = OffensePanelUiFactory.CreateVerticalRoot(
            panelObject.transform,
            "OffenseExpeditionMembers",
            new Vector2(0f, 0f),
            new Vector2(0.43f, 0.88f),
            new Vector2(24f, 24f),
            new Vector2(-12f, -24f),
            8f);

        GameObject detail = OffensePanelUiFactory.CreateText(panelObject.transform, "OffenseExpeditionDetail", 19f, TextAlignmentOptions.TopLeft, tmpKoreanFontService);
        RectTransform detailRect = detail.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0.45f, 0f);
        detailRect.anchorMax = new Vector2(1f, 0.88f);
        detailRect.offsetMin = new Vector2(12f, 24f);
        detailRect.offsetMax = new Vector2(-24f, -24f);

        OffenseExpeditionPanel panel = panelObject.AddComponent<OffenseExpeditionPanel>();
        panel.BindGeneratedView(
            header.GetComponent<TMP_Text>(),
            detail.GetComponent<TMP_Text>(),
            memberRootObject.GetComponent<RectTransform>());
        objectResolver.Inject(panel);
        runtimeReferences.RegisterExpeditionPanel(panel);
        return panel;
    }
}
