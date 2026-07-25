using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VContainer.Unity;

public sealed class DungeonTitleCanvasProvider : IDungeonUiCanvasProvider
{
    private readonly SceneUiBootstrapReferences runtimeReferences;
    private Canvas canvas;

    public DungeonTitleCanvasProvider(
        SceneUiBootstrapReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public Canvas GetOrCreateCanvas()
    {
        if (canvas != null)
        {
            return canvas;
        }

        EnsureEventSystem();
        GameObject canvasObject = new GameObject(
            "TitleCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private void EnsureEventSystem()
    {
        if (runtimeReferences.EventSystem != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
        runtimeReferences.RegisterEventSystem(
            eventSystemObject.GetComponent<EventSystem>());
    }
}

public sealed class DungeonTitleUiController : IStartable, IDisposable
{
    private const float ConfirmationSeconds = 4f;

    private readonly IDungeonSaveSlotCatalog slotCatalog;
    private readonly IDungeonSceneNavigator sceneNavigator;
    private readonly IDungeonUiCanvasProvider canvasProvider;
    private readonly ITmpKoreanFontService fontService;
    private readonly IDungeonSettingsUi settingsUi;
    private readonly IUiClock uiClock;
    private readonly IGameTimeScaleController timeScaleController;
    private readonly IResourcesAssetLoader resourcesAssetLoader;

    private readonly Dictionary<string, SlotView> slots = new Dictionary<string, SlotView>();
    private GameObject runtimeRoot;
    private GameObject titleMainScreen;
    private Button continueButton;
    private TMP_Text statusText;
    private GameObject difficultyModal;
    private GameObject newRunConfirmModal;
    private DungeonDifficulty selectedDifficulty = DungeonDifficulty.Normal;
    private TMP_Text difficultyNameText;
    private TMP_Text difficultyMultiplierText;
    private TMP_Text difficultyDescriptionText;
    private TMP_Text difficultyEmblemText;
    private TMP_Text difficultyWarningText;
    private readonly List<DifficultyRowView> difficultyRows = new List<DifficultyRowView>();
    private string confirmationKey = string.Empty;
    private float confirmationExpiresAt;
    private bool newRunWillOverwriteSaves;

    public DungeonTitleUiController(
        IDungeonSaveSlotCatalog slotCatalog,
        IDungeonSceneNavigator sceneNavigator,
        IDungeonUiCanvasProvider canvasProvider,
        ITmpKoreanFontService fontService,
        IDungeonSettingsUi settingsUi,
        IUiClock uiClock,
        IGameTimeScaleController timeScaleController,
        IResourcesAssetLoader resourcesAssetLoader)
    {
        this.slotCatalog = slotCatalog ?? throw new ArgumentNullException(nameof(slotCatalog));
        this.sceneNavigator = sceneNavigator ?? throw new ArgumentNullException(nameof(sceneNavigator));
        this.canvasProvider = canvasProvider ?? throw new ArgumentNullException(nameof(canvasProvider));
        this.fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));
        this.settingsUi = settingsUi ?? throw new ArgumentNullException(nameof(settingsUi));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.timeScaleController = timeScaleController
            ?? throw new ArgumentNullException(nameof(timeScaleController));
        this.resourcesAssetLoader = resourcesAssetLoader
            ?? throw new ArgumentNullException(nameof(resourcesAssetLoader));
    }

    public void Start()
    {
        Canvas canvas = canvasProvider.GetOrCreateCanvas();
        CreateTitleScreen(canvas.transform);
        RefreshSlots();
        string transitionMessage = sceneNavigator.ConsumeTitleMessage();
        SetStatus(
            string.IsNullOrWhiteSpace(transitionMessage)
                ? "저장된 던전을 이어가거나 새로운 사장을 선택하세요."
                : transitionMessage,
            !string.IsNullOrWhiteSpace(transitionMessage));
        DungeonUiThemeRuntime.Ensure(canvas, fontService, uiClock).ApplyNow();
    }

    public void Dispose()
    {
        if (runtimeRoot != null)
        {
            UnityEngine.Object.Destroy(runtimeRoot);
        }
    }

    private void CreateTitleScreen(Transform parent)
    {
        runtimeRoot = new GameObject("DungeonTitleRuntimeUI", typeof(RectTransform));
        runtimeRoot.transform.SetParent(parent, false);
        Stretch(runtimeRoot.GetComponent<RectTransform>());

        titleMainScreen = new GameObject("TitleMainScreen", typeof(RectTransform));
        titleMainScreen.transform.SetParent(runtimeRoot.transform, false);
        Stretch(titleMainScreen.GetComponent<RectTransform>());

        Image background = CreateImage(titleMainScreen.transform, "TitleBackground", DungeonUiTheme.SurfaceMuted);
        Stretch(background.rectTransform);
        background.raycastTarget = true;

        Image topBand = CreateImage(titleMainScreen.transform, "TitleTopBand", DungeonUiTheme.Surface);
        SetRect(topBand.rectTransform, new Vector2(0f, 0.9f), Vector2.one, Vector2.zero, Vector2.zero);

        Image accentBand = CreateImage(titleMainScreen.transform, "TitleAccentBand", DungeonUiTheme.Accent);
        SetRect(accentBand.rectTransform, new Vector2(0f, 0.895f), new Vector2(1f, 0.9f), Vector2.zero, Vector2.zero);

        Transform brand = CreatePanel(titleMainScreen.transform, "TitleBrand", new Vector2(0.07f, 0.14f), new Vector2(0.48f, 0.84f), false);
        CreateBranding(brand);

        Transform savePanel = CreatePanel(titleMainScreen.transform, "TitleSavePanel", new Vector2(0.52f, 0.14f), new Vector2(0.93f, 0.84f), true);
        CreateSavePanel(savePanel);
        CreateDifficultyModal(runtimeRoot.transform);

        TMP_Text version = CreateText(titleMainScreen.transform, "VersionText", Application.version, 15f, TextAlignmentOptions.BottomRight);
        SetRect(version.rectTransform, new Vector2(0.76f, 0.035f), new Vector2(0.93f, 0.09f), Vector2.zero, Vector2.zero);
        version.color = DungeonUiTheme.TextSecondary;
    }

    private void CreateBranding(Transform parent)
    {
        GameObject iconObject = new GameObject("BrandIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(parent, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        SetRect(iconRect, new Vector2(0f, 0.77f), new Vector2(0.18f, 1f), Vector2.zero, Vector2.zero);
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = resourcesAssetLoader.LoadOptional<Sprite>("Branding/DungeonStoryIcon");
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text title = CreateText(parent, "Title", "DungeonStory", 58f, TextAlignmentOptions.BottomLeft);
        SetRect(title.rectTransform, new Vector2(0.21f, 0.82f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        title.fontStyle = FontStyles.Bold;

        TMP_Text subtitle = CreateText(parent, "Subtitle", "지하에 방을 짓고, 살아 움직이는 던전을 운영하세요.", 22f, TextAlignmentOptions.TopLeft);
        SetRect(subtitle.rectTransform, new Vector2(0.21f, 0.7f), new Vector2(1f, 0.82f), Vector2.zero, Vector2.zero);
        subtitle.color = DungeonUiTheme.TextSecondary;

        continueButton = CreateButton(parent, "ContinueLatestButton", "이어하기", ContinueLatest,
            new Vector2(0f, 0.48f), new Vector2(0.72f, 0.59f), selected: true);
        CreateButton(parent, "StartNewRunButton", "새 게임", StartNewGame,
            new Vector2(0f, 0.34f), new Vector2(0.72f, 0.45f));
        CreateButton(parent, "StartupSettingsButton", "설정", settingsUi.Show,
            new Vector2(0f, 0.2f), new Vector2(0.72f, 0.31f));
        CreateButton(parent, "StartupQuitButton", "종료", QuitGame,
            new Vector2(0f, 0.06f), new Vector2(0.72f, 0.17f), destructive: true);
    }

    private void CreateSavePanel(Transform parent)
    {
        TMP_Text heading = CreateText(parent, "SaveHeading", "저장된 던전", 30f, TextAlignmentOptions.MidlineLeft);
        SetRect(heading.rectTransform, new Vector2(0.055f, 0.88f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero);
        heading.fontStyle = FontStyles.Bold;

        TMP_Text hint = CreateText(parent, "SaveHint", "최근 저장 기록", 16f, TextAlignmentOptions.MidlineLeft);
        SetRect(hint.rectTransform, new Vector2(0.055f, 0.81f), new Vector2(0.95f, 0.88f), Vector2.zero, Vector2.zero);
        hint.color = DungeonUiTheme.TextSecondary;

        CreateSlot(parent, DungeonGameSaveSlotService.ManualSaveSlot, "수동 저장", 0.59f, 0.79f);
        CreateSlot(parent, DungeonGameSaveSlotService.QuickSaveSlot, "빠른 저장", 0.37f, 0.57f);
        CreateSlot(parent, DungeonGameSaveSlotService.AutoSaveSlot, "자동 저장", 0.15f, 0.35f);

        statusText = CreateText(parent, "TitleStatus", string.Empty, 16f, TextAlignmentOptions.TopLeft);
        SetRect(statusText.rectTransform, new Vector2(0.055f, 0.025f), new Vector2(0.95f, 0.125f), Vector2.zero, Vector2.zero);
        statusText.textWrappingMode = TextWrappingModes.Normal;
    }

    private void CreateSlot(Transform parent, string slotId, string label, float bottom, float top)
    {
        Transform row = CreatePanel(parent, "SaveSlot_" + slotId, new Vector2(0.055f, bottom), new Vector2(0.945f, top), false);
        row.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;

        TMP_Text name = CreateText(row, "SlotName", label, 20f, TextAlignmentOptions.TopLeft);
        SetRect(name.rectTransform, new Vector2(0.035f, 0.51f), new Vector2(0.55f, 0.92f), Vector2.zero, Vector2.zero);
        name.fontStyle = FontStyles.Bold;

        TMP_Text metadata = CreateText(row, "Metadata", string.Empty, 15f, TextAlignmentOptions.TopLeft);
        SetRect(metadata.rectTransform, new Vector2(0.035f, 0.08f), new Vector2(0.62f, 0.54f), Vector2.zero, Vector2.zero);
        metadata.color = DungeonUiTheme.TextSecondary;

        Button load = CreateButton(row, "LoadButton_" + slotId, "불러오기", () => LoadSlot(slotId),
            new Vector2(0.64f, 0.2f), new Vector2(0.84f, 0.8f));
        Button delete = CreateButton(row, "DeleteButton_" + slotId, "삭제", () => DeleteSlot(slotId),
            new Vector2(0.86f, 0.2f), new Vector2(0.97f, 0.8f), destructive: true);
        slots.Add(slotId, new SlotView(metadata, load, delete));
    }

    private void ContinueLatest()
    {
        DungeonSaveSlotInfo latest = LatestValidSlot();
        if (latest == null)
        {
            SetStatus("이어갈 수 있는 저장 데이터가 없습니다.", true);
            return;
        }

        BeginLoad(latest.SlotId);
    }

    private void StartNewGame()
    {
        newRunWillOverwriteSaves = slots.Keys.Any(slotCatalog.HasSave);

        if (!ShowDifficultySelection())
        {
            SetStatus("이미 화면을 전환하고 있거나 Gameplay 씬을 찾을 수 없습니다.", true);
        }
    }

    private bool ShowDifficultySelection()
    {
        ClearConfirmation();
        SelectDifficulty(selectedDifficulty);
        UpdateDifficultyWarning();
        if (titleMainScreen != null)
        {
            titleMainScreen.SetActive(false);
        }

        difficultyModal.SetActive(true);
        return true;
    }

    private void BeginNewGame(DungeonDifficulty difficulty)
    {
        difficultyModal.SetActive(false);
        if (!sceneNavigator.StartNewGame(difficulty))
        {
            ShowTitleMainScreen();
            SetStatus("게임 화면으로 전환할 수 없습니다.", true);
        }
    }

    private void ShowTitleMainScreen()
    {
        if (difficultyModal != null)
        {
            difficultyModal.SetActive(false);
        }

        if (titleMainScreen != null)
        {
            titleMainScreen.SetActive(true);
        }

        newRunWillOverwriteSaves = false;
    }

    private void ShowNewRunConfirmModal()
    {
        ClearConfirmation();
        SetStatus(string.Empty, false);
        newRunConfirmModal.SetActive(true);
    }

    private void ConfirmNewRunOverwrite()
    {
        newRunConfirmModal.SetActive(false);
        if (!ShowDifficultySelection())
        {
            SetStatus("이미 화면을 전환하고 있거나 Gameplay 씬을 찾을 수 없습니다.", true);
        }
    }

    private void CreateNewRunConfirmModal(Transform parent)
    {
        newRunConfirmModal = new GameObject("NewRunConfirmModal", typeof(RectTransform));
        newRunConfirmModal.transform.SetParent(parent, false);
        Stretch(newRunConfirmModal.GetComponent<RectTransform>());

        Image scrim = CreateImage(newRunConfirmModal.transform, "NewRunConfirmScrim", DungeonUiTheme.ModalScrim);
        Stretch(scrim.rectTransform);
        scrim.raycastTarget = true;

        Transform panel = CreatePanel(
            newRunConfirmModal.transform,
            "NewRunConfirmPanel",
            new Vector2(0.34f, 0.35f),
            new Vector2(0.66f, 0.65f),
            true);

        TMP_Text heading = CreateText(panel, "NewRunConfirmHeading", "새 게임 시작", 30f, TextAlignmentOptions.MidlineLeft);
        SetRect(heading.rectTransform, new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);
        heading.fontStyle = FontStyles.Bold;

        TMP_Text body = CreateText(
            panel,
            "NewRunConfirmBody",
            "새 게임을 시작하면 기존 실행 저장이 지워집니다.\n계속 진행할까요?",
            20f,
            TextAlignmentOptions.TopLeft);
        SetRect(body.rectTransform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.7f), Vector2.zero, Vector2.zero);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.color = DungeonUiTheme.TextSecondary;

        CreateButton(panel, "NewRunConfirmCancelButton", "취소", () => newRunConfirmModal.SetActive(false),
            new Vector2(0.08f, 0.12f), new Vector2(0.42f, 0.28f));
        CreateButton(panel, "NewRunConfirmConfirmButton", "새 게임", ConfirmNewRunOverwrite,
            new Vector2(0.58f, 0.12f), new Vector2(0.92f, 0.28f), selected: true, destructive: true);
        newRunConfirmModal.SetActive(false);
    }

    private void CreateDifficultyModal(Transform parent)
    {
        difficultyModal = new GameObject("DifficultyModal", typeof(RectTransform));
        difficultyModal.transform.SetParent(parent, false);
        Stretch(difficultyModal.GetComponent<RectTransform>());

        Image scrim = CreateImage(difficultyModal.transform, "DifficultyScreenBackground", DungeonUiTheme.SurfaceMuted);
        Stretch(scrim.rectTransform);
        scrim.raycastTarget = true;

        Transform panel = CreatePanel(
            difficultyModal.transform,
            "DifficultyPanel",
            new Vector2(0.18f, 0.13f),
            new Vector2(0.82f, 0.87f),
            true);
        panel.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.03f, 0.96f);

        TMP_Text heading = CreateText(panel, "DifficultyHeading", "난이도 선택", 34f, TextAlignmentOptions.MidlineLeft);
        SetRect(heading.rectTransform, new Vector2(0.055f, 0.89f), new Vector2(0.42f, 0.97f), Vector2.zero, Vector2.zero);
        heading.fontStyle = FontStyles.Bold;
        heading.text = "난이도 선택";

        Transform listPanel = CreatePanel(panel, "DifficultyList", new Vector2(0.045f, 0.18f), new Vector2(0.32f, 0.84f), false);
        listPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.38f);
        CreateDifficultyRow(listPanel, "DifficultyEasyButton", DungeonDifficulty.Easy, "쉬움", "적 80%", 0.69f);
        CreateDifficultyRow(listPanel, "DifficultyNormalButton", DungeonDifficulty.Normal, "보통", "기본", 0.47f);
        CreateDifficultyRow(listPanel, "DifficultyHardButton", DungeonDifficulty.Hard, "어려움", "적 125%", 0.25f);

        Image divider = CreateImage(panel, "DifficultyDivider", DungeonUiTheme.TextSecondary);
        SetRect(divider.rectTransform, new Vector2(0.36f, 0.19f), new Vector2(0.362f, 0.84f), Vector2.zero, Vector2.zero);
        divider.color = new Color(DungeonUiTheme.TextSecondary.r, DungeonUiTheme.TextSecondary.g, DungeonUiTheme.TextSecondary.b, 0.6f);

        difficultyEmblemText = CreateText(panel, "DifficultyEmblem", "翼", 84f, TextAlignmentOptions.Center);
        SetRect(difficultyEmblemText.rectTransform, new Vector2(0.72f, 0.67f), new Vector2(0.9f, 0.88f), Vector2.zero, Vector2.zero);
        difficultyEmblemText.color = DungeonUiTheme.TextPrimary;

        difficultyNameText = CreateText(panel, "DifficultyName", string.Empty, 34f, TextAlignmentOptions.MidlineRight);
        SetRect(difficultyNameText.rectTransform, new Vector2(0.56f, 0.58f), new Vector2(0.9f, 0.68f), Vector2.zero, Vector2.zero);
        difficultyNameText.fontStyle = FontStyles.Bold;

        difficultyMultiplierText = CreateText(panel, "DifficultyExperience", string.Empty, 24f, TextAlignmentOptions.MidlineLeft);
        SetRect(difficultyMultiplierText.rectTransform, new Vector2(0.055f, 0.08f), new Vector2(0.28f, 0.16f), Vector2.zero, Vector2.zero);
        difficultyMultiplierText.fontStyle = FontStyles.Bold;
        difficultyMultiplierText.color = DungeonUiTheme.Accent;

        difficultyDescriptionText = CreateText(panel, "DifficultyDescription", string.Empty, 20f, TextAlignmentOptions.TopRight);
        SetRect(difficultyDescriptionText.rectTransform, new Vector2(0.43f, 0.24f), new Vector2(0.9f, 0.54f), Vector2.zero, Vector2.zero);
        difficultyDescriptionText.textWrappingMode = TextWrappingModes.Normal;

        difficultyWarningText = CreateText(panel, "DifficultyOverwriteWarning", string.Empty, 17f, TextAlignmentOptions.MidlineLeft);
        SetRect(difficultyWarningText.rectTransform, new Vector2(0.43f, 0.13f), new Vector2(0.9f, 0.2f), Vector2.zero, Vector2.zero);
        difficultyWarningText.color = DungeonUiTheme.Warning;
        difficultyWarningText.textWrappingMode = TextWrappingModes.Normal;

        CreateButton(panel, "DifficultyCancelButton", "이전", () => difficultyModal.SetActive(false),
            new Vector2(0.055f, 0.03f), new Vector2(0.18f, 0.11f));
        CreateButton(panel, "DifficultyNextButton", "다음", () => BeginNewGame(selectedDifficulty),
            new Vector2(0.78f, 0.03f), new Vector2(0.94f, 0.11f), selected: true);
        SelectDifficulty(DungeonDifficulty.Normal);
        difficultyModal.SetActive(false);
    }

    private void CreateDifficultyRow(
        Transform parent,
        string buttonName,
        DungeonDifficulty difficulty,
        string label,
        string multiplier,
        float bottom)
    {
        Button button = CreateButton(
            parent,
            buttonName,
            $"{label}\n{multiplier}",
            () => SelectDifficulty(difficulty),
            new Vector2(0.06f, bottom),
            new Vector2(0.94f, bottom + 0.16f),
            selectedDifficulty == difficulty);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.fontSize = 18f;
            text.fontSizeMin = 13f;
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        difficultyRows.Add(new DifficultyRowView(difficulty, button));
    }

    private void SelectDifficulty(DungeonDifficulty difficulty)
    {
        selectedDifficulty = DungeonDifficultyRules.Normalize((int)difficulty);
        foreach (DifficultyRowView row in difficultyRows)
        {
            if (row?.Button == null)
            {
                continue;
            }

            row.Button.image.color = row.Difficulty == selectedDifficulty
                ? new Color(DungeonUiTheme.TextPrimary.r, DungeonUiTheme.TextPrimary.g, DungeonUiTheme.TextPrimary.b, 0.26f)
                : new Color(0f, 0f, 0f, 0.28f);
        }

        if (difficultyNameText != null)
        {
            difficultyNameText.text = DifficultyNameText(selectedDifficulty);
        }

        if (difficultyMultiplierText != null)
        {
            difficultyMultiplierText.text = DifficultyModifierTextLocalized(selectedDifficulty);
        }

        if (difficultyDescriptionText != null)
        {
            difficultyDescriptionText.text = DifficultyDescriptionLocalized(selectedDifficulty);
        }

        UpdateDifficultyWarning();

        if (difficultyEmblemText != null)
        {
            difficultyEmblemText.text = selectedDifficulty == DungeonDifficulty.Hard
                ? "III"
                : selectedDifficulty == DungeonDifficulty.Easy
                    ? "I"
                    : "II";
        }
    }

    private void UpdateDifficultyWarning()
    {
        if (difficultyWarningText == null)
        {
            return;
        }

        difficultyWarningText.text = newRunWillOverwriteSaves
            ? "다음을 누르면 기존 실행 저장을 지우고 새 게임을 시작합니다."
            : string.Empty;
        difficultyWarningText.gameObject.SetActive(newRunWillOverwriteSaves);
    }

    private static string DifficultyNameText(DungeonDifficulty difficulty)
    {
        return difficulty switch
        {
            DungeonDifficulty.Easy => "쉬움",
            DungeonDifficulty.Hard => "어려움",
            _ => "보통"
        };
    }

    private static string DifficultyRowSubtitle(DungeonDifficulty difficulty)
    {
        return difficulty switch
        {
            DungeonDifficulty.Easy => "적 80%",
            DungeonDifficulty.Hard => "적 125%",
            _ => "기본"
        };
    }

    private static string DifficultyModifierTextLocalized(DungeonDifficulty difficulty)
    {
        return difficulty switch
        {
            DungeonDifficulty.Easy => "전투 보정  적 체력 80% · 공격 80%",
            DungeonDifficulty.Hard => "전투 보정  적 체력 125% · 공격 120% · 주도권 110%",
            _ => "전투 보정  기본 수치"
        };
    }

    private static string DifficultyDescriptionLocalized(DungeonDifficulty difficulty)
    {
        return difficulty switch
        {
            DungeonDifficulty.Easy =>
                "1. 적 전투 수치가 낮아집니다.\n2. 초반 침입 압박이 완만합니다.\n3. 시스템을 익히기 좋은 난이도입니다.",
            DungeonDifficulty.Hard =>
                "1. 적 체력 125%, 공격 120%, 주도권 110%.\n2. 침입과 오펜스 실패 압박이 큽니다.\n3. 장비와 회복 순환을 적극적으로 요구합니다.",
            _ =>
                "1. 기본 전투 수치로 시작합니다.\n2. 운영, 방어, 오펜스를 고르게 요구합니다.\n3. 권장 기준 난이도입니다."
        };
    }

    private static string DifficultyName(DungeonDifficulty difficulty)
    {
        return difficulty switch
        {
            DungeonDifficulty.Easy => "쉬움",
            DungeonDifficulty.Hard => "어려움",
            _ => "보통"
        };
    }

    private static string DifficultyModifierText(DungeonDifficulty difficulty)
    {
        return difficulty switch
        {
            DungeonDifficulty.Easy => "전투 보정  적 체력 80% · 공격 80%",
            DungeonDifficulty.Hard => "전투 보정  적 체력 125% · 공격 120% · 주도권 110%",
            _ => "전투 보정  기본 수치"
        };
    }

    private static string DifficultyDescription(DungeonDifficulty difficulty)
    {
        return difficulty switch
        {
            DungeonDifficulty.Easy =>
                "1. 적 체력과 공격력이 낮아집니다.\n2. 초반 침입 압박이 완만합니다.\n3. 시작 파티 실험에 적합합니다.",
            DungeonDifficulty.Hard =>
                "1. 적 체력 125%, 공격 120%, 주도권 110%.\n2. 침입과 오펜스 손실 압박이 큽니다.\n3. 장비와 회복 순환을 전제로 합니다.",
            _ =>
                "1. 기본 전투 수치로 시작합니다.\n2. 운영, 방어, 오펜스를 고르게 요구합니다.\n3. 권장 기준 난이도입니다."
        };
    }

    private void LoadSlot(string slotId)
    {
        if (!slotCatalog.HasSave(slotId))
        {
            SetStatus("선택한 저장 데이터가 없습니다.", true);
            RefreshSlots();
            return;
        }

        BeginLoad(slotId);
    }

    private void BeginLoad(string slotId)
    {
        if (!sceneNavigator.LoadGame(slotId))
        {
            SetStatus("이미 화면을 전환하고 있거나 Gameplay 씬을 찾을 수 없습니다.", true);
        }
    }

    private void DeleteSlot(string slotId)
    {
        if (!slotCatalog.HasSave(slotId))
        {
            RefreshSlots();
            return;
        }

        if (!Confirm("delete-" + slotId, "삭제하려면 같은 버튼을 한 번 더 누르세요."))
        {
            return;
        }

        slotCatalog.Delete(slotId);
        ClearConfirmation();
        RefreshSlots();
        SetStatus("저장 데이터를 삭제했습니다.", false);
    }

    private void RefreshSlots()
    {
        Dictionary<string, DungeonSaveSlotInfo> current = slotCatalog.GetSlots()
            .GroupBy(slot => slot.SlotId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (KeyValuePair<string, SlotView> pair in slots)
        {
            current.TryGetValue(pair.Key, out DungeonSaveSlotInfo info);
            bool valid = info != null && info.IsValid;
            pair.Value.Metadata.text = FormatMetadata(info);
            pair.Value.Load.interactable = valid;
            pair.Value.Delete.interactable = info != null;
        }

        if (continueButton != null)
        {
            continueButton.interactable = LatestValidSlot() != null;
        }
    }

    private DungeonSaveSlotInfo LatestValidSlot()
    {
        return slotCatalog.GetSlots()
            .Where(slot => slot.IsValid)
            .OrderByDescending(slot => ParseSavedAt(slot.SavedAtUtc))
            .FirstOrDefault();
    }

    private static string FormatMetadata(DungeonSaveSlotInfo info)
    {
        if (info == null)
        {
            return "비어 있음";
        }

        if (!info.IsValid)
        {
            return "읽을 수 없는 저장 데이터";
        }

        DateTime timestamp = ParseSavedAt(info.SavedAtUtc);
        string date = timestamp == DateTime.MinValue
            ? "저장 시각 없음"
            : timestamp.ToLocalTime().ToString("M월 d일 HH:mm", CultureInfo.CurrentCulture);
        string debugBadge = info.DebugModified ? " · 디버그 사용" : string.Empty;
        return $"{date}\n{Mathf.Max(1, info.Day)}일차 · {Mathf.Max(0, info.Money):N0} 골드{debugBadge}";
    }

    private static DateTime ParseSavedAt(string value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed)
            ? parsed
            : DateTime.MinValue;
    }

    private bool Confirm(string key, string message)
    {
        if (confirmationKey == key && uiClock.Time <= confirmationExpiresAt)
        {
            return true;
        }

        confirmationKey = key;
        confirmationExpiresAt = uiClock.Time + ConfirmationSeconds;
        SetStatus(message, true);
        return false;
    }

    private void ClearConfirmation()
    {
        confirmationKey = string.Empty;
        confirmationExpiresAt = 0f;
    }

    private void SetStatus(string message, bool warning)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = message ?? string.Empty;
        statusText.color = warning ? DungeonUiTheme.Warning : DungeonUiTheme.TextSecondary;
    }

    private void QuitGame()
    {
        timeScaleController.Scale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private Button CreateButton(
        Transform parent,
        string name,
        string label,
        UnityEngine.Events.UnityAction action,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool selected = false,
        bool destructive = false)
    {
        label = LocalizeButtonLabel(name, label);
        if (string.Equals(name, "DifficultyCancelButton", StringComparison.Ordinal))
        {
            action = ShowTitleMainScreen;
        }

        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(action);
        TMP_Text text = CreateText(buttonObject.transform, "Label", label, 19f, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        DungeonUiTheme.StyleButton(button, selected, destructive);
        return button;
    }

    private static string LocalizeButtonLabel(string name, string fallback)
    {
        return name switch
        {
            "ContinueLatestButton" => "이어하기",
            "StartNewRunButton" => "새 게임",
            "StartupSettingsButton" => "설정",
            "StartupQuitButton" => "종료",
            "DifficultyEasyButton" => DifficultyNameText(DungeonDifficulty.Easy) + "\n" + DifficultyRowSubtitle(DungeonDifficulty.Easy),
            "DifficultyNormalButton" => DifficultyNameText(DungeonDifficulty.Normal) + "\n" + DifficultyRowSubtitle(DungeonDifficulty.Normal),
            "DifficultyHardButton" => DifficultyNameText(DungeonDifficulty.Hard) + "\n" + DifficultyRowSubtitle(DungeonDifficulty.Hard),
            "DifficultyCancelButton" => "이전",
            "DifficultyNextButton" => "다음",
            _ when name != null && name.StartsWith("LoadButton_", StringComparison.Ordinal) => "불러오기",
            _ when name != null && name.StartsWith("DeleteButton_", StringComparison.Ordinal) => "삭제",
            _ => fallback
        };
    }

    private TMP_Text CreateText(Transform parent, string name, string value, float size, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        text.fontSizeMin = Mathf.Max(12f, size * 0.72f);
        text.fontSizeMax = size;
        text.enableAutoSizing = true;
        text.alignment = alignment;
        text.color = DungeonUiTheme.TextPrimary;
        text.raycastTarget = false;
        fontService.Apply(text);
        return text;
    }

    private static Transform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, bool raised)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        SetRect(panel.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        panel.GetComponent<Image>().color = raised ? DungeonUiTheme.Panel : DungeonUiTheme.Surface;
        return panel.transform;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private sealed class SlotView
    {
        public SlotView(TMP_Text metadata, Button load, Button delete)
        {
            Metadata = metadata;
            Load = load;
            Delete = delete;
        }

        public TMP_Text Metadata { get; }
        public Button Load { get; }
        public Button Delete { get; }
    }

    private sealed class DifficultyRowView
    {
        public DifficultyRowView(DungeonDifficulty difficulty, Button button)
        {
            Difficulty = difficulty;
            Button = button;
        }

        public DungeonDifficulty Difficulty { get; }
        public Button Button { get; }
    }
}
