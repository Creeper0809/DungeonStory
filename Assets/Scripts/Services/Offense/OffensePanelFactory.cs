using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
            new Vector2(1600f, 900f));
        GameObject panelObject = OffensePanelUiFactory.CreatePanel(
            canvasObject.transform,
            "OffenseWorldMapPanel",
            new Vector2(0.02f, 0.08f),
            new Vector2(0.98f, 0.92f),
            new Color(0.025f, 0.03f, 0.04f, 0.98f));

        GameObject header = OffensePanelUiFactory.CreateText(panelObject.transform, "OffenseWorldMapHeader", 25f, TextAlignmentOptions.Left, tmpKoreanFontService);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(24f, -66f);
        headerRect.offsetMax = new Vector2(-24f, -18f);

        GameObject buttonViewportObject = new GameObject(
            "OffenseWorldMapActionViewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D),
            typeof(ScrollRect));
        buttonViewportObject.transform.SetParent(panelObject.transform, false);
        RectTransform buttonViewportRect =
            buttonViewportObject.GetComponent<RectTransform>();
        buttonViewportRect.anchorMin = new Vector2(0.73f, 0.34f);
        buttonViewportRect.anchorMax = new Vector2(1f, 0.9f);
        buttonViewportRect.offsetMin = new Vector2(12f, 12f);
        buttonViewportRect.offsetMax = new Vector2(-20f, -10f);
        buttonViewportObject.GetComponent<Image>().color =
            new Color(0.045f, 0.052f, 0.06f, 0.92f);

        GameObject buttonRootObject = OffensePanelUiFactory.CreateVerticalRoot(
            buttonViewportObject.transform,
            "OffenseWorldMapTargets",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(8f, 0f),
            new Vector2(-8f, 0f),
            8f);
        RectTransform buttonRootRect =
            buttonRootObject.GetComponent<RectTransform>();
        buttonRootRect.pivot = new Vector2(0.5f, 1f);
        buttonRootRect.anchoredPosition = Vector2.zero;
        ContentSizeFitter buttonContentFitter =
            buttonRootObject.AddComponent<ContentSizeFitter>();
        buttonContentFitter.verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect buttonScrollRect =
            buttonViewportObject.GetComponent<ScrollRect>();
        buttonScrollRect.viewport = buttonViewportRect;
        buttonScrollRect.content = buttonRootRect;
        buttonScrollRect.horizontal = false;
        buttonScrollRect.vertical = true;
        buttonScrollRect.movementType = ScrollRect.MovementType.Clamped;
        buttonScrollRect.inertia = true;
        buttonScrollRect.decelerationRate = 0.12f;

        GameObject detail = OffensePanelUiFactory.CreateText(panelObject.transform, "OffenseWorldMapDetail", 20f, TextAlignmentOptions.TopLeft, tmpKoreanFontService);
        RectTransform detailRect = detail.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0.73f, 0f);
        detailRect.anchorMax = new Vector2(1f, 0.32f);
        detailRect.offsetMin = new Vector2(12f, 14f);
        detailRect.offsetMax = new Vector2(-20f, -8f);

        GameObject viewportObject = new GameObject(
            "OffenseStrategicMapViewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D),
            typeof(ScrollRect),
            typeof(OffenseStrategicMapInput));
        viewportObject.transform.SetParent(panelObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(0.72f, 0.9f);
        viewportRect.offsetMin = new Vector2(20f, 16f);
        viewportRect.offsetMax = new Vector2(-8f, -10f);
        viewportObject.GetComponent<Image>().color =
            new Color(0.045f, 0.055f, 0.065f, 1f);

        GameObject mapContentObject = new GameObject(
            "OffenseStrategicMapContent",
            typeof(RectTransform));
        mapContentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform mapContentRect =
            mapContentObject.GetComponent<RectTransform>();
        mapContentRect.anchorMin = new Vector2(0.5f, 0.5f);
        mapContentRect.anchorMax = new Vector2(0.5f, 0.5f);
        mapContentRect.pivot = new Vector2(0.5f, 0.5f);
        mapContentRect.sizeDelta = new Vector2(1040f, 720f);

        ScrollRect scrollRect = viewportObject.GetComponent<ScrollRect>();
        scrollRect.content = mapContentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = true;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.12f;
        viewportObject.GetComponent<OffenseStrategicMapInput>()
            .Bind(mapContentRect);

        CombatCardClashPresenter clashPresenter =
            panelObject.AddComponent<CombatCardClashPresenter>();
        clashPresenter.Bind(
            panelObject.GetComponent<RectTransform>(),
            tmpKoreanFontService);

        OffenseWorldMapPanel panel = panelObject.AddComponent<OffenseWorldMapPanel>();
        panel.BindGeneratedView(
            header.GetComponent<TMP_Text>(),
            detail.GetComponent<TMP_Text>(),
            buttonRootObject.GetComponent<RectTransform>());
        OffenseWorldMapResponsiveLayout responsiveLayout =
            panelObject.AddComponent<OffenseWorldMapResponsiveLayout>();
        responsiveLayout.Bind(
            headerRect,
            viewportRect,
            mapContentRect,
            buttonViewportRect,
            detailRect);
        panel.BindStrategicGeneratedView(mapContentRect, responsiveLayout);
        objectResolver.Inject(panel);
        objectResolver.Inject(clashPresenter);
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
