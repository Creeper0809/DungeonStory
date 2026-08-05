using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer.Unity;
using DungeonStory.Foundation;

public sealed class DungeonSettingsUiController :
    IDungeonSettingsUi,
    IStartable,
    IDisposable
{
    private const int SettingsSortingOrder = 940;

    private readonly IDungeonUserSettingsService settingsService;
    private readonly IDungeonUiCanvasProvider canvasProvider;
    private readonly ITmpKoreanFontService fontService;
    private readonly DungeonUserSettingsRuntimeTargets runtimeTargets;
    private readonly IUiClock uiClock;
    private readonly IGameSpeedController gameSpeedController;

    private readonly List<Image> themedSurfaces = new List<Image>();
    private readonly List<Button> tabButtons = new List<Button>();
    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();

    private Canvas canvas;
    private GameObject runtimeRoot;
    private GameObject modalRoot;
    private Image scrimImage;
    private Image panelImage;
    private GameObject[] pages;
    private Button optionButton;
    private Button closeButton;
    private TMP_Text statusText;
    private TMP_Text windowModeValue;
    private TMP_Text resolutionValue;
    private TMP_Text cameraControlsValue;
    private TMP_Text cameraSpeedValue;
    private TMP_Text masterVolumeValue;
    private TMP_Text musicVolumeValue;
    private TMP_Text effectsVolumeValue;
    private TMP_Text uiVolumeValue;
    private TMP_Text uiScaleValue;
    private TMP_Text textScaleValue;
    private TMP_Text maxCarryMultiplierValue;
    private TMP_Text defenseTimeResponseValue;
    private Slider cameraSpeedSlider;
    private Slider masterVolumeSlider;
    private Slider musicVolumeSlider;
    private Slider effectsVolumeSlider;
    private Slider uiVolumeSlider;
    private Slider uiScaleSlider;
    private Slider textScaleSlider;
    private Slider maxCarryMultiplierSlider;
    private Toggle edgeScrollToggle;
    private Toggle pauseOnResearchTreeToggle;
    private Toggle highContrastToggle;
    private Toggle reducedMotionToggle;
    private Toggle developerModeToggle;
    private DungeonSettingsHotkeyBehaviour hotkeyBehaviour;
    private bool pauseCaptured;
    private bool wasPaused;
    private int activePage;

    public DungeonSettingsUiController(
        IDungeonUserSettingsService settingsService,
        IDungeonUiCanvasProvider canvasProvider,
        ITmpKoreanFontService fontService,
        DungeonUserSettingsRuntimeTargets runtimeTargets,
        IUiClock uiClock,
        IGameSpeedController gameSpeedController)
    {
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.canvasProvider = canvasProvider ?? throw new ArgumentNullException(nameof(canvasProvider));
        this.fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));
        this.runtimeTargets = runtimeTargets
            ?? throw new ArgumentNullException(nameof(runtimeTargets));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.gameSpeedController = gameSpeedController
            ?? throw new ArgumentNullException(nameof(gameSpeedController));
    }

    public bool IsVisible => modalRoot != null && modalRoot.activeInHierarchy;

    public void Start()
    {
        canvas = canvasProvider.GetOrCreateCanvas();
        BuildResolutionList();
        CreateModal();
        BindOptionButton();
        RefreshControls();
    }

    public void Dispose()
    {
        RestorePause();
        if (optionButton != null)
        {
            optionButton.onClick.RemoveListener(Show);
        }

        if (runtimeRoot != null)
        {
            UnityEngine.Object.Destroy(runtimeRoot);
        }
    }

    public void Show()
    {
        if (modalRoot == null || IsVisible)
        {
            return;
        }

        CapturePause();
        SetPaused(true);
        RefreshControls();
        ShowPage(activePage);
        modalRoot.SetActive(true);
        runtimeRoot.transform.SetAsLastSibling();
        hotkeyBehaviour.enabled = true;
    }

    public void Close()
    {
        if (modalRoot == null || !modalRoot.activeSelf)
        {
            return;
        }

        modalRoot.SetActive(false);
        hotkeyBehaviour.enabled = false;
        RestorePause();
    }

    private void BindOptionButton()
    {
        Transform upperRight = canvas.transform.Find("UpperRightPanel");
        optionButton = upperRight != null
            ? upperRight.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.GetComponentInChildren<TMP_Text>(true)?.text == "옵션")
            : null;
        if (optionButton == null)
        {
            return;
        }

        optionButton.name = "SettingsMenuButton";
        optionButton.onClick.RemoveListener(Show);
        optionButton.onClick.AddListener(Show);
    }

    private void CreateModal()
    {
        runtimeRoot = new GameObject(
            "DungeonSettingsRuntimeUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster),
            typeof(DungeonSettingsHotkeyBehaviour));
        runtimeRoot.transform.SetParent(canvas.transform, false);
        Stretch(runtimeRoot.GetComponent<RectTransform>());
        Canvas overlay = runtimeRoot.GetComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = SettingsSortingOrder;

        hotkeyBehaviour = runtimeRoot.GetComponent<DungeonSettingsHotkeyBehaviour>();
        hotkeyBehaviour.Initialize(Close);
        hotkeyBehaviour.enabled = false;

        modalRoot = new GameObject("SettingsModal", typeof(RectTransform));
        modalRoot.transform.SetParent(runtimeRoot.transform, false);
        Stretch(modalRoot.GetComponent<RectTransform>());

        GameObject scrim = new GameObject("InputBlocker", typeof(RectTransform), typeof(Image), typeof(Button));
        scrim.transform.SetParent(modalRoot.transform, false);
        Stretch(scrim.GetComponent<RectTransform>());
        scrimImage = scrim.GetComponent<Image>();
        scrim.GetComponent<Button>().transition = Selectable.Transition.None;
        scrim.GetComponent<Button>().onClick.AddListener(Close);

        GameObject panel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(modalRoot.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(900f, 760f);
        panelImage = panel.GetComponent<Image>();

        TMP_Text title = CreateText(
            panel.transform,
            "Title",
            "설정",
            32f,
            TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f));
        SetOffsets(title.rectTransform, new Vector2(30f, -72f), new Vector2(-90f, -18f));
        title.fontStyle = FontStyles.Bold;

        closeButton = CreateButton(
            panel.transform,
            "SettingsCloseButton",
            "X",
            Close,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-74f, -66f),
            new Vector2(-24f, -18f));

        GameObject tabBar = new GameObject("SettingsTabs", typeof(RectTransform));
        tabBar.transform.SetParent(panel.transform, false);
        RectTransform tabBarRect = tabBar.GetComponent<RectTransform>();
        tabBarRect.anchorMin = new Vector2(0f, 1f);
        tabBarRect.anchorMax = new Vector2(1f, 1f);
        SetOffsets(tabBarRect, new Vector2(30f, -132f), new Vector2(-30f, -82f));

        tabButtons.Add(CreateButton(tabBar.transform, "DisplaySettingsTab", "화면", () => ShowPage(0),
            new Vector2(0f, 0f), new Vector2(0.25f, 1f), Vector2.zero, new Vector2(-4f, 0f)));
        tabButtons.Add(CreateButton(tabBar.transform, "AudioSettingsTab", "오디오", () => ShowPage(1),
            new Vector2(0.25f, 0f), new Vector2(0.5f, 1f), new Vector2(4f, 0f), new Vector2(-4f, 0f)));
        tabButtons.Add(CreateButton(tabBar.transform, "AccessibilitySettingsTab", "접근성", () => ShowPage(2),
            new Vector2(0.5f, 0f), new Vector2(0.75f, 1f), new Vector2(4f, 0f), new Vector2(-4f, 0f)));
        tabButtons.Add(CreateButton(tabBar.transform, "DeveloperSettingsTab", "개발", () => ShowPage(3),
            new Vector2(0.75f, 0f), new Vector2(1f, 1f), new Vector2(4f, 0f), Vector2.zero));

        pages = new GameObject[4];
        for (int index = 0; index < pages.Length; index++)
        {
            pages[index] = new GameObject($"SettingsPage_{index}", typeof(RectTransform));
            pages[index].transform.SetParent(panel.transform, false);
            RectTransform pageRect = pages[index].GetComponent<RectTransform>();
            pageRect.anchorMin = Vector2.zero;
            pageRect.anchorMax = Vector2.one;
            SetOffsets(pageRect, new Vector2(30f, 92f), new Vector2(-30f, -148f));
        }

        CreateDisplayPage(pages[0].transform);
        CreateAudioPage(pages[1].transform);
        CreateAccessibilityPage(pages[2].transform);
        CreateDefenseTimeResponseRow(pages[2].transform);
        CreateDevelopmentPage(pages[3].transform);

        statusText = CreateText(
            panel.transform,
            "SettingsStatus",
            string.Empty,
            15f,
            TextAlignmentOptions.MidlineLeft,
            Vector2.zero,
            new Vector2(0.68f, 0f));
        SetOffsets(statusText.rectTransform, new Vector2(30f, 24f), new Vector2(-8f, 76f));

        CreateButton(panel.transform, "ResetSettingsButton", "기본값", ResetDefaults,
            new Vector2(0.7f, 0f), new Vector2(0.84f, 0f), new Vector2(0f, 24f), new Vector2(-5f, 76f));
        CreateButton(panel.transform, "ApplySettingsButton", "완료", Close,
            new Vector2(0.84f, 0f), Vector2.right, new Vector2(5f, 24f), new Vector2(-30f, 76f), selected: true);

        ApplyTheme();
        ShowPage(0);
        modalRoot.SetActive(false);
    }

    private void CreateDisplayPage(Transform page)
    {
        windowModeValue = CreateCycleRow(page, "WindowMode", "창 모드", 16f,
            () => CycleWindowMode(-1), () => CycleWindowMode(1));
        resolutionValue = CreateCycleRow(page, "Resolution", "해상도", 94f,
            () => CycleResolution(-1), () => CycleResolution(1));
        cameraSpeedSlider = CreateSliderRow(page, "CameraSpeed", "카메라 속도", 172f,
            0.5f, 2f, value => UpdateSetting(data => data.cameraSpeed = value), out cameraSpeedValue);
        cameraControlsValue = CreateCycleRow(page, "CameraControls", "카메라 키", 250f,
            () => CycleCameraControls(-1), () => CycleCameraControls(1));
        edgeScrollToggle = CreateToggleRow(page, "EdgeScroll", "화면 가장자리 이동", 328f,
            value => UpdateSetting(data => data.edgeScroll = value));
        maxCarryMultiplierSlider = CreateSliderRow(page, "MaxCarryMultiplier", "최대 운반 배율", 406f,
            1f, 2.5f, value => UpdateSetting(data => data.maxCarryMultiplier = QuantizeCarryMultiplier(value)),
            out maxCarryMultiplierValue);
        pauseOnResearchTreeToggle = CreateToggleRow(
            page,
            "PauseOnResearchTree",
            "연구 화면 자동 일시정지",
            484f,
            value => UpdateSetting(data => data.pauseOnResearchTree = value));
    }

    private void CreateAudioPage(Transform page)
    {
        masterVolumeSlider = CreateSliderRow(page, "MasterVolume", "전체 음량", 16f,
            0f, 1f, value => UpdateSetting(data => data.masterVolume = value), out masterVolumeValue);
        musicVolumeSlider = CreateSliderRow(page, "MusicVolume", "음악", 94f,
            0f, 1f, value => UpdateSetting(data => data.musicVolume = value), out musicVolumeValue);
        effectsVolumeSlider = CreateSliderRow(page, "EffectsVolume", "효과음", 172f,
            0f, 1f, value => UpdateSetting(data => data.effectsVolume = value), out effectsVolumeValue);
        uiVolumeSlider = CreateSliderRow(page, "UiVolume", "UI", 250f,
            0f, 1f, value => UpdateSetting(data => data.uiVolume = value), out uiVolumeValue);
    }

    private void CreateAccessibilityPage(Transform page)
    {
        uiScaleSlider = CreateSliderRow(page, "UiScale", "UI 크기", 16f,
            0.8f, 1.25f, value => UpdateSetting(data => data.uiScale = value), out uiScaleValue);
        textScaleSlider = CreateSliderRow(page, "TextScale", "글자 크기", 94f,
            0.9f, 1.25f, value => UpdateSetting(data => data.textScale = value), out textScaleValue);
        highContrastToggle = CreateToggleRow(page, "HighContrast", "고대비", 172f,
            value => UpdateSetting(data => data.highContrast = value));
        reducedMotionToggle = CreateToggleRow(page, "ReducedMotion", "모션 감소", 250f,
            value => UpdateSetting(data => data.reducedMotion = value));
    }

    private void CreateDefenseTimeResponseRow(Transform page)
    {
        defenseTimeResponseValue = CreateCycleRow(
            page,
            "DefenseTimeResponse",
            "침입 시 시간 반응",
            328f,
            () => CycleDefenseTimeResponse(-1),
            () => CycleDefenseTimeResponse(1));
    }

    private void CreateDevelopmentPage(Transform page)
    {
        developerModeToggle = CreateToggleRow(
            page,
            "DeveloperMode",
            "개발자 모드",
            16f,
            value => UpdateSetting(data => data.developerMode = value));

        TMP_Text description = CreateText(
            page,
            "DeveloperModeDescription",
            "활성화하면 게임 화면 중상단에 디버그 팔레트 버튼이 나타납니다.",
            18f,
            TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f));
        SetOffsets(
            description.rectTransform,
            new Vector2(18f, -184f),
            new Vector2(-18f, -100f));
        description.textWrappingMode = TextWrappingModes.Normal;
        description.color = DungeonUiTheme.TextSecondary;

        TMP_Text warning = CreateText(
            page,
            "DeveloperModeWarning",
            "상태를 바꾸는 디버그 명령이나 치트를 사용한 런은 저장 슬롯에 '디버그 사용'으로 표시됩니다.",
            18f,
            TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f));
        SetOffsets(
            warning.rectTransform,
            new Vector2(18f, -286f),
            new Vector2(-18f, -202f));
        warning.textWrappingMode = TextWrappingModes.Normal;
        warning.color = DungeonUiTheme.Warning;
    }

    private void ShowPage(int index)
    {
        activePage = Mathf.Clamp(index, 0, pages.Length - 1);
        for (int pageIndex = 0; pageIndex < pages.Length; pageIndex++)
        {
            pages[pageIndex].SetActive(pageIndex == activePage);
            DungeonUiTheme.StyleButton(tabButtons[pageIndex], pageIndex == activePage);
        }
    }

    private void CycleWindowMode(int direction)
    {
        int count = Enum.GetValues(typeof(DungeonWindowMode)).Length;
        UpdateSetting(data => data.windowMode = (DungeonWindowMode)Wrap((int)data.windowMode + direction, count));
    }

    private void CycleResolution(int direction)
    {
        DungeonUserSettingsData current = settingsService.Current;
        int index = resolutions.FindIndex(value =>
            value.x == current.resolutionWidth && value.y == current.resolutionHeight);
        index = Wrap((index < 0 ? 0 : index) + direction, resolutions.Count);
        Vector2Int resolution = resolutions[index];
        UpdateSetting(data =>
        {
            data.resolutionWidth = resolution.x;
            data.resolutionHeight = resolution.y;
        });
    }

    private void CycleCameraControls(int direction)
    {
        int count = Enum.GetValues(typeof(DungeonCameraControlScheme)).Length;
        UpdateSetting(data => data.cameraControls = (DungeonCameraControlScheme)Wrap(
            (int)data.cameraControls + direction,
            count));
    }

    private void CycleDefenseTimeResponse(int direction)
    {
        int count = Enum.GetValues(typeof(DungeonDefenseTimeResponse)).Length;
        UpdateSetting(data =>
            data.defenseTimeResponse = (DungeonDefenseTimeResponse)Wrap(
                (int)data.defenseTimeResponse + direction,
                count));
    }

    private void UpdateSetting(Action<DungeonUserSettingsData> change)
    {
        settingsService.Update(change);
        RefreshControls();
        ApplyTheme();
    }

    private void ResetDefaults()
    {
        settingsService.ResetDefaults();
        RefreshControls();
        ApplyTheme();
    }

    private void RefreshControls()
    {
        DungeonUserSettingsData data = settingsService.Current;
        if (windowModeValue == null)
        {
            return;
        }

        windowModeValue.text = data.windowMode switch
        {
            DungeonWindowMode.Windowed => "창 모드",
            DungeonWindowMode.ExclusiveFullscreen => "전체 화면",
            _ => "테두리 없는 창"
        };
        resolutionValue.text = $"{data.resolutionWidth} x {data.resolutionHeight}";
        cameraControlsValue.text = data.cameraControls switch
        {
            DungeonCameraControlScheme.WasdOnly => "WASD",
            DungeonCameraControlScheme.ArrowsOnly => "방향키",
            _ => "WASD + 방향키"
        };

        SetSlider(cameraSpeedSlider, cameraSpeedValue, data.cameraSpeed, $"x{data.cameraSpeed:0.0}");
        SetSlider(masterVolumeSlider, masterVolumeValue, data.masterVolume, Percent(data.masterVolume));
        SetSlider(musicVolumeSlider, musicVolumeValue, data.musicVolume, Percent(data.musicVolume));
        SetSlider(effectsVolumeSlider, effectsVolumeValue, data.effectsVolume, Percent(data.effectsVolume));
        SetSlider(uiVolumeSlider, uiVolumeValue, data.uiVolume, Percent(data.uiVolume));
        SetSlider(uiScaleSlider, uiScaleValue, data.uiScale, Percent(data.uiScale));
        SetSlider(textScaleSlider, textScaleValue, data.textScale, Percent(data.textScale));
        SetSlider(maxCarryMultiplierSlider, maxCarryMultiplierValue, data.maxCarryMultiplier, $"x{data.maxCarryMultiplier:0.00}");
        edgeScrollToggle.SetIsOnWithoutNotify(data.edgeScroll);
        pauseOnResearchTreeToggle?.SetIsOnWithoutNotify(data.pauseOnResearchTree);
        highContrastToggle.SetIsOnWithoutNotify(data.highContrast);
        reducedMotionToggle.SetIsOnWithoutNotify(data.reducedMotion);
        if (defenseTimeResponseValue != null)
        {
            defenseTimeResponseValue.text = data.defenseTimeResponse switch
            {
                DungeonDefenseTimeResponse.PauseOnCritical =>
                    "치명 사건마다 자동 정지",
                DungeonDefenseTimeResponse.KeepCurrent =>
                    "현재 배속 유지",
                _ => "첫 진입 시 X1"
            };
        }
        developerModeToggle?.SetIsOnWithoutNotify(data.developerMode);
        statusText.text = settingsService.LastError;
        statusText.color = string.IsNullOrWhiteSpace(settingsService.LastError)
            ? DungeonUiTheme.TextSecondary
            : DungeonUiTheme.Danger;
    }

    private void ApplyTheme()
    {
        if (scrimImage != null)
        {
            scrimImage.color = DungeonUiTheme.ModalScrim;
        }

        if (panelImage != null)
        {
            panelImage.color = DungeonUiTheme.Panel;
        }

        foreach (Image surface in themedSurfaces.Where(surface => surface != null))
        {
            surface.color = DungeonUiTheme.Surface;
        }

        foreach (Button button in runtimeRoot.GetComponentsInChildren<Button>(true))
        {
            int tabIndex = tabButtons.IndexOf(button);
            DungeonUiTheme.StyleButton(button, tabIndex >= 0 && tabIndex == activePage);
        }

        foreach (TMP_Text text in runtimeRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            text.color = text == statusText && !string.IsNullOrWhiteSpace(settingsService.LastError)
                ? DungeonUiTheme.Danger
                : DungeonUiTheme.TextPrimary;
        }

        DungeonUiThemeRuntime.Ensure(
            canvas,
            fontService,
            uiClock,
            settingsService).ApplyNow();
    }

    private TMP_Text CreateCycleRow(
        Transform parent,
        string name,
        string label,
        float top,
        UnityAction previous,
        UnityAction next)
    {
        RectTransform row = CreateRow(parent, name + "Row", top);
        CreateRowLabel(row, label);
        CreateButton(row, name + "Previous", "<", previous,
            new Vector2(0.5f, 0.15f), new Vector2(0.58f, 0.85f), Vector2.zero, new Vector2(-4f, 0f));
        TMP_Text value = CreateText(row, name + "Value", string.Empty, 18f,
            TextAlignmentOptions.Center, new Vector2(0.58f, 0.15f), new Vector2(0.88f, 0.85f));
        CreateButton(row, name + "Next", ">", next,
            new Vector2(0.88f, 0.15f), new Vector2(0.96f, 0.85f), new Vector2(4f, 0f), Vector2.zero);
        return value;
    }

    private Slider CreateSliderRow(
        Transform parent,
        string name,
        string label,
        float top,
        float minimum,
        float maximum,
        UnityAction<float> changed,
        out TMP_Text valueText)
    {
        RectTransform row = CreateRow(parent, name + "Row", top);
        CreateRowLabel(row, label);
        Slider slider = CreateSlider(row, name, new Vector2(0.5f, 0.27f), new Vector2(0.86f, 0.73f));
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.onValueChanged.AddListener(changed);
        valueText = CreateText(row, name + "Value", string.Empty, 17f,
            TextAlignmentOptions.MidlineRight, new Vector2(0.87f, 0f), new Vector2(0.96f, 1f));
        return slider;
    }

    private Toggle CreateToggleRow(
        Transform parent,
        string name,
        string label,
        float top,
        UnityAction<bool> changed)
    {
        RectTransform row = CreateRow(parent, name + "Row", top);
        CreateRowLabel(row, label);
        GameObject toggleObject = new GameObject(name + "Toggle", typeof(RectTransform), typeof(Toggle));
        toggleObject.transform.SetParent(row, false);
        RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
        toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(0.92f, 0.5f);
        toggleRect.sizeDelta = new Vector2(34f, 34f);

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(toggleObject.transform, false);
        Stretch(background.GetComponent<RectTransform>());
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = DungeonUiTheme.SurfaceRaised;

        GameObject checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkmark.transform.SetParent(background.transform, false);
        RectTransform checkRect = checkmark.GetComponent<RectTransform>();
        Stretch(checkRect);
        checkRect.offsetMin = new Vector2(7f, 7f);
        checkRect.offsetMax = new Vector2(-7f, -7f);
        Image checkImage = checkmark.GetComponent<Image>();
        checkImage.color = DungeonUiTheme.Accent;

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkImage;
        toggle.onValueChanged.AddListener(changed);
        return toggle;
    }

    private RectTransform CreateRow(Transform parent, string name, float top)
    {
        GameObject rowObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        rowObject.transform.SetParent(parent, false);
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        SetOffsets(row, new Vector2(0f, -top - 64f), new Vector2(0f, -top));
        Image image = rowObject.GetComponent<Image>();
        image.color = DungeonUiTheme.Surface;
        themedSurfaces.Add(image);
        return row;
    }

    private void CreateRowLabel(Transform row, string label)
    {
        TMP_Text text = CreateText(row, "Label", label, 19f,
            TextAlignmentOptions.MidlineLeft, Vector2.zero, new Vector2(0.48f, 1f));
        SetOffsets(text.rectTransform, new Vector2(18f, 0f), new Vector2(-8f, 0f));
        text.fontStyle = FontStyles.Bold;
    }

    private Slider CreateSlider(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject sliderObject = new GameObject(name + "Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = anchorMin;
        sliderRect.anchorMax = anchorMax;
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(sliderObject.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.36f);
        backgroundRect.anchorMax = new Vector2(1f, 0.64f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = DungeonUiTheme.SurfaceMuted;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        Stretch(fillArea.GetComponent<RectTransform>());
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        Stretch(fill.GetComponent<RectTransform>());
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = DungeonUiTheme.Accent;

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform, false);
        Stretch(handleArea.GetComponent<RectTransform>());
        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 30f);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = DungeonUiTheme.TextPrimary;

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private Button CreateButton(
        Transform parent,
        string name,
        string label,
        UnityAction action,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        bool selected = false)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(action);
        CreateText(buttonObject.transform, "Label", label, 17f,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
        DungeonUiTheme.StyleButton(button, selected);
        return button;
    }

    private TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        fontService.Apply(text);
        text.text = value;
        text.fontSize = fontSize;
        text.fontSizeMin = Mathf.Max(12f, fontSize - 5f);
        text.fontSizeMax = fontSize;
        text.enableAutoSizing = true;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = alignment;
        text.characterSpacing = 0f;
        return text;
    }

    private void CapturePause()
    {
        if (pauseCaptured)
        {
            return;
        }

        wasPaused = gameSpeedController.IsPaused;
        pauseCaptured = true;
    }

    private void SetPaused(bool paused)
    {
        gameSpeedController.SetPaused(paused);
    }

    private void RestorePause()
    {
        if (!pauseCaptured)
        {
            return;
        }

        gameSpeedController.SetPaused(wasPaused);
        pauseCaptured = false;
    }

    private void BuildResolutionList()
    {
        resolutions.Clear();
        resolutions.AddRange(DungeonSettingsResolutionCatalog.Build());
    }

    private static void SetSlider(Slider slider, TMP_Text label, float value, string display)
    {
        slider.SetValueWithoutNotify(value);
        label.text = display;
    }

    private static string Percent(float value)
    {
        return $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private static float QuantizeCarryMultiplier(float value)
    {
        return Mathf.Clamp(Mathf.Round(value / 0.05f) * 0.05f, 1f, 2.5f);
    }

    private static int Wrap(int value, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        return (value % count + count) % count;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetOffsets(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
