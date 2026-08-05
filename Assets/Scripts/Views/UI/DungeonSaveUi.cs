using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VContainer.Unity;

public sealed class DungeonSaveUiPresentationContext
{
    public DungeonSaveUiPresentationContext(
        IDungeonUiCanvasProvider canvasProvider,
        ITmpKoreanFontService fontService,
        IUiClock uiClock,
        IDungeonUserSettingsService userSettings)
    {
        CanvasProvider = canvasProvider
            ?? throw new ArgumentNullException(nameof(canvasProvider));
        FontService = fontService
            ?? throw new ArgumentNullException(nameof(fontService));
        UiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        UserSettings = userSettings
            ?? throw new ArgumentNullException(nameof(userSettings));
    }

    public IDungeonUiCanvasProvider CanvasProvider { get; }
    public ITmpKoreanFontService FontService { get; }
    public IUiClock UiClock { get; }
    public IDungeonUserSettingsService UserSettings { get; }
}

public sealed class DungeonSaveUiSessionContext
{
    public DungeonSaveUiSessionContext(
        IGameSessionStateProvider gameDataProvider,
        DungeonSceneRuntimeReferences sceneReferences,
        IGameSpeedController gameSpeedController,
        IOwnerRunManagerProvider ownerRunManagerProvider)
    {
        GameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        SceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
        GameSpeedController = gameSpeedController
            ?? throw new ArgumentNullException(nameof(gameSpeedController));
        OwnerRunManagerProvider = ownerRunManagerProvider
            ?? throw new ArgumentNullException(nameof(ownerRunManagerProvider));
    }

    public IGameSessionStateProvider GameDataProvider { get; }
    public DungeonSceneRuntimeReferences SceneReferences { get; }
    public IGameSpeedController GameSpeedController { get; }
    public IOwnerRunManagerProvider OwnerRunManagerProvider { get; }
}

public sealed class DungeonSaveUiActionContext
{
    public DungeonSaveUiActionContext(
        IDungeonGameSaveSlotService slotService,
        IDungeonSaveCommandService saveCommandService,
        IDungeonSettingsUi settingsUi,
        IDungeonSceneNavigator sceneNavigator)
    {
        SlotService = slotService
            ?? throw new ArgumentNullException(nameof(slotService));
        SaveCommandService = saveCommandService
            ?? throw new ArgumentNullException(nameof(saveCommandService));
        SettingsUi = settingsUi
            ?? throw new ArgumentNullException(nameof(settingsUi));
        SceneNavigator = sceneNavigator
            ?? throw new ArgumentNullException(nameof(sceneNavigator));
    }

    public IDungeonGameSaveSlotService SlotService { get; }
    public IDungeonSaveCommandService SaveCommandService { get; }
    public IDungeonSettingsUi SettingsUi { get; }
    public IDungeonSceneNavigator SceneNavigator { get; }
}

public sealed class DungeonSaveUiController : IStartable, IDisposable
{
    private const string RuntimeRootName = "DungeonSaveRuntimeUI";
    private const float ConfirmationSeconds = 4f;

    private readonly IDungeonGameSaveSlotService slotService;
    private readonly IDungeonUiCanvasProvider canvasProvider;
    private readonly ITmpKoreanFontService fontService;
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly DungeonSceneRuntimeReferences sceneReferences;
    private readonly IUiClock uiClock;
    private readonly IGameSpeedController gameSpeedController;
    private readonly IOwnerRunManagerProvider ownerRunManagerProvider;
    private readonly IDungeonSaveCommandService saveCommandService;
    private readonly IDungeonSettingsUi settingsUi;
    private readonly IDungeonSceneNavigator sceneNavigator;
    private readonly IDungeonUserSettingsService userSettings;

    private readonly Dictionary<string, SlotRow> slotRows = new Dictionary<string, SlotRow>();
    private Canvas canvas;
    private Button menuButton;
    private GameObject runtimeRoot;
    private GameObject modalRoot;
    private GameObject inGameActions;
    private Button closeButton;
    private TMP_Text titleText;
    private TMP_Text statusText;
    private bool pauseWasCaptured;
    private bool wasPausedBeforeModal;
    private string pendingConfirmationKey = string.Empty;
    private float confirmationExpiresAt;

    public DungeonSaveUiController(
        DungeonSaveUiPresentationContext presentation,
        DungeonSaveUiSessionContext session,
        DungeonSaveUiActionContext actions)
    {
        presentation = presentation
            ?? throw new ArgumentNullException(nameof(presentation));
        session = session ?? throw new ArgumentNullException(nameof(session));
        actions = actions ?? throw new ArgumentNullException(nameof(actions));
        canvasProvider = presentation.CanvasProvider;
        fontService = presentation.FontService;
        uiClock = presentation.UiClock;
        userSettings = presentation.UserSettings;
        gameDataProvider = session.GameDataProvider;
        sceneReferences = session.SceneReferences;
        gameSpeedController = session.GameSpeedController;
        ownerRunManagerProvider = session.OwnerRunManagerProvider;
        slotService = actions.SlotService;
        saveCommandService = actions.SaveCommandService;
        settingsUi = actions.SettingsUi;
        sceneNavigator = actions.SceneNavigator;
    }

    public void Start()
    {
        canvas = canvasProvider.GetOrCreateCanvas();
        CreateMenuButton();
        CreateModal();
        RefreshSlots();
    }

    public void Dispose()
    {
        RestorePauseState();
        if (menuButton != null)
        {
            UnityEngine.Object.Destroy(menuButton.gameObject);
        }

        if (runtimeRoot != null)
        {
            UnityEngine.Object.Destroy(runtimeRoot);
        }
    }

    private void CreateMenuButton()
    {
        Transform upperRight = canvas.transform.Find("UpperRightPanel");
        if (upperRight == null)
        {
            throw new InvalidOperationException("Save UI requires UpperRightPanel under the main Canvas.");
        }

        GameObject buttonObject = new GameObject("SaveMenuButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(upperRight, false);
        menuButton = buttonObject.GetComponent<Button>();
        menuButton.onClick.AddListener(ShowSaveMenu);
        CreateText(buttonObject.transform, "Label", "저장", 18f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
        DungeonUiTheme.StyleButton(menuButton);
        DungeonUiThemeRuntime.Ensure(
            canvas,
            fontService,
            uiClock,
            userSettings).ApplyNow();
    }

    private void CreateModal()
    {
        runtimeRoot = new GameObject(RuntimeRootName, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        runtimeRoot.transform.SetParent(canvas.transform, false);
        Stretch(runtimeRoot.GetComponent<RectTransform>());
        Canvas overlayCanvas = runtimeRoot.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = Mathf.Max(900, canvas.sortingOrder + 10);

        modalRoot = new GameObject("SaveModal", typeof(RectTransform));
        modalRoot.transform.SetParent(runtimeRoot.transform, false);
        Stretch(modalRoot.GetComponent<RectTransform>());

        GameObject scrim = new GameObject("InputBlocker", typeof(RectTransform), typeof(Image), typeof(Button));
        scrim.transform.SetParent(modalRoot.transform, false);
        Stretch(scrim.GetComponent<RectTransform>());
        scrim.GetComponent<Image>().color = DungeonUiTheme.ModalScrim;
        scrim.GetComponent<Button>().transition = Selectable.Transition.None;
        scrim.GetComponent<Button>().onClick.AddListener(TryCloseFromScrim);

        GameObject panel = new GameObject("SavePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(modalRoot.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(820f, 760f);
        panel.GetComponent<Image>().color = DungeonUiTheme.Panel;

        titleText = CreateText(panel.transform, "Title", "저장 및 불러오기", 30f,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(1f, 1f));
        SetOffsets(titleText.rectTransform, new Vector2(28f, -74f), new Vector2(-90f, -18f));
        titleText.fontStyle = FontStyles.Bold;

        closeButton = CreateButton(panel.transform, "CloseButton", "X", CloseModal,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, -64f), new Vector2(-22f, -20f));

        CreateSlotRow(panel.transform, DungeonGameSaveSlotService.ManualSaveSlot, "수동 저장", 160f, true);
        CreateSlotRow(panel.transform, DungeonGameSaveSlotService.QuickSaveSlot, "빠른 저장", 300f, true);
        CreateSlotRow(panel.transform, DungeonGameSaveSlotService.AutoSaveSlot, "자동 저장", 440f, false);

        statusText = CreateText(panel.transform, "StatusText", string.Empty, 17f,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0f), new Vector2(1f, 0f));
        SetOffsets(statusText.rectTransform, new Vector2(28f, 22f), new Vector2(-28f, 76f));
        statusText.color = DungeonUiTheme.TextSecondary;
        statusText.textWrappingMode = TextWrappingModes.Normal;

        inGameActions = new GameObject("InGameActions", typeof(RectTransform));
        inGameActions.transform.SetParent(panel.transform, false);
        RectTransform inGameActionsRect = inGameActions.GetComponent<RectTransform>();
        inGameActionsRect.anchorMin = new Vector2(0f, 0f);
        inGameActionsRect.anchorMax = new Vector2(1f, 0f);
        SetOffsets(inGameActionsRect, new Vector2(28f, 88f), new Vector2(-28f, 144f));
        CreateButton(inGameActions.transform, "InGameSettingsButton", "설정", settingsUi.Show,
            new Vector2(0f, 0f), new Vector2(0.32f, 1f), Vector2.zero, new Vector2(-5f, 0f));
        CreateButton(inGameActions.transform, "ReturnToTitleButton", "타이틀로", ReturnToTitle,
            new Vector2(0.32f, 0f), new Vector2(0.68f, 1f), new Vector2(5f, 0f), new Vector2(-5f, 0f));
        CreateButton(inGameActions.transform, "InGameQuitButton", "게임 종료", QuitGame,
            new Vector2(0.68f, 0f), new Vector2(1f, 1f), new Vector2(5f, 0f), Vector2.zero, destructive: true);

        inGameActions.SetActive(true);
        closeButton.gameObject.SetActive(true);

        modalRoot.SetActive(false);
    }

    private void CreateSlotRow(Transform parent, string slotId, string displayName, float topOffset, bool canSave)
    {
        GameObject rowObject = new GameObject("SaveSlot_" + slotId, typeof(RectTransform), typeof(Image));
        rowObject.transform.SetParent(parent, false);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        SetOffsets(rowRect, new Vector2(28f, -topOffset - 118f), new Vector2(-28f, -topOffset));
        rowObject.GetComponent<Image>().color = DungeonUiTheme.Surface;

        TMP_Text name = CreateText(rowObject.transform, "SlotName", displayName, 21f,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.55f), new Vector2(0.52f, 1f));
        SetOffsets(name.rectTransform, new Vector2(18f, 0f), new Vector2(-8f, -4f));
        name.fontStyle = FontStyles.Bold;

        TMP_Text metadata = CreateText(rowObject.transform, "Metadata", string.Empty, 16f,
            TextAlignmentOptions.TopLeft, new Vector2(0f, 0f), new Vector2(0.58f, 0.58f));
        SetOffsets(metadata.rectTransform, new Vector2(18f, 8f), new Vector2(-8f, 0f));
        metadata.color = DungeonUiTheme.TextSecondary;

        float actionLeft = canSave ? 0.58f : 0.68f;
        Button saveButton = canSave
            ? CreateButton(rowObject.transform, "SaveButton_" + slotId, "저장", () => SaveSlot(slotId),
                new Vector2(actionLeft, 0.18f), new Vector2(0.72f, 0.82f), Vector2.zero, new Vector2(-5f, 0f), selected: true)
            : null;
        Button loadButton = CreateButton(rowObject.transform, "LoadButton_" + slotId, "불러오기", () => LoadSlot(slotId),
            new Vector2(canSave ? 0.72f : actionLeft, 0.18f), new Vector2(0.89f, 0.82f), new Vector2(5f, 0f), new Vector2(-5f, 0f));
        Button deleteButton = CreateButton(rowObject.transform, "DeleteButton_" + slotId, "삭제", () => DeleteSlot(slotId),
            new Vector2(0.89f, 0.18f), new Vector2(1f, 0.82f), new Vector2(5f, 0f), Vector2.zero, destructive: true);

        slotRows.Add(slotId, new SlotRow(metadata, saveButton, loadButton, deleteButton));
    }

    private void ShowSaveMenu()
    {
        titleText.text = "저장 및 불러오기";
        inGameActions.SetActive(true);
        closeButton.gameObject.SetActive(true);
        SetStatus("진행 상황을 저장하거나 이전 상태를 불러올 수 있습니다.", false);
        RefreshSlots();
        ShowModal();
    }

    private void ShowModal()
    {
        CapturePauseState();
        SetPaused(true);
        modalRoot.SetActive(true);
        modalRoot.transform.SetAsLastSibling();
    }

    private void CloseModal()
    {
        modalRoot.SetActive(false);
        ClearConfirmation();
        RestorePauseState();
    }

    private void TryCloseFromScrim()
    {
        CloseModal();
    }

    private void ReturnToTitle()
    {
        if (!Confirm("return-title", "현재 진행을 저장하고 타이틀로 돌아가려면 한 번 더 누르세요."))
        {
            return;
        }

        if (ownerRunManagerProvider.TryGetManager(out OwnerRunManager ownerManager)
            && ownerManager.CurrentOwnerActor != null
            && !saveCommandService.TryAutoSave(out string message))
        {
            SetStatus("타이틀로 돌아가기 전에 저장하지 못했습니다: " + message, true);
            return;
        }

        ClearConfirmation();
        RestorePauseState();
        if (!sceneNavigator.LoadTitle())
        {
            SetStatus("이미 화면을 전환하고 있거나 타이틀 씬을 찾을 수 없습니다.", true);
        }
    }

    private void QuitGame()
    {
        if (!Confirm("quit", "현재 진행을 저장하고 종료하려면 한 번 더 누르세요."))
        {
            return;
        }

        if (ownerRunManagerProvider.TryGetManager(out OwnerRunManager ownerManager)
            && ownerManager.CurrentOwnerActor != null
            && !saveCommandService.TryAutoSave(out string message))
        {
            SetStatus("종료 전에 저장하지 못했습니다: " + message, true);
            return;
        }

        gameSpeedController.SetSpeed(1);
        gameSpeedController.SetPaused(false);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SaveSlot(string slotId)
    {
        if (slotService.HasSave(slotId) && !Confirm("save:" + slotId, "저장된 진행을 덮어쓰려면 저장을 한 번 더 누르세요."))
        {
            return;
        }

        try
        {
            slotService.Save(slotId);
            ClearConfirmation();
            RefreshSlots();
            SetStatus("현재 진행을 저장했습니다.", false);
        }
        catch (Exception exception)
        {
            SetStatus("저장하지 못했습니다: " + exception.Message, true);
        }
    }

    private void LoadSlot(string slotId)
    {
        if (!slotService.TryLoad(slotId, out DungeonGameRestoreReport report))
        {
            string message = report.Errors.Count > 0 ? string.Join(" / ", report.Errors) : "알 수 없는 오류";
            SetStatus("불러오지 못했습니다: " + message, true);
            RefreshSlots();
            return;
        }

        sceneReferences.OwnerSelectionPanel?.RefreshVisibility();
        modalRoot.SetActive(false);
        ClearConfirmation();
        RestorePauseState();
    }

    private void DeleteSlot(string slotId)
    {
        if (!slotService.HasSave(slotId))
        {
            SetStatus("삭제할 저장이 없습니다.", true);
            return;
        }

        if (!Confirm("delete:" + slotId, "삭제하려면 삭제를 한 번 더 누르세요."))
        {
            return;
        }

        bool deleted = slotService.Delete(slotId);
        ClearConfirmation();
        RefreshSlots();
        SetStatus(deleted ? "저장을 삭제했습니다." : "저장을 삭제하지 못했습니다.", !deleted);
    }

    private bool Confirm(string key, string prompt)
    {
        if (pendingConfirmationKey == key && uiClock.Time <= confirmationExpiresAt)
        {
            return true;
        }

        pendingConfirmationKey = key;
        confirmationExpiresAt = uiClock.Time + ConfirmationSeconds;
        SetStatus(prompt, false);
        return false;
    }

    private void ClearConfirmation()
    {
        pendingConfirmationKey = string.Empty;
        confirmationExpiresAt = 0f;
    }

    private void RefreshSlots()
    {
        bool canSaveCurrentRun = ownerRunManagerProvider.TryGetManager(out OwnerRunManager ownerManager)
            && ownerManager != null
            && ownerManager.CurrentOwnerActor != null;
        Dictionary<string, DungeonSaveSlotInfo> infoById = slotService.GetSlots()
            .ToDictionary(info => info.SlotId, StringComparer.Ordinal);
        foreach (KeyValuePair<string, SlotRow> pair in slotRows)
        {
            bool exists = infoById.TryGetValue(pair.Key, out DungeonSaveSlotInfo info);
            bool valid = exists && info.IsValid;
            pair.Value.Metadata.text = valid
                ? FormatSlot(info)
                : exists
                    ? (string.IsNullOrWhiteSpace(info.IncompatibilityReason)
                        ? "호환되지 않는 저장"
                        : info.IncompatibilityReason)
                    : "비어 있음";
            pair.Value.Load.interactable = valid;
            pair.Value.Delete.interactable = exists;
            if (pair.Value.Save != null)
            {
                pair.Value.Save.interactable = canSaveCurrentRun;
            }
        }
    }

    private static string FormatSlot(DungeonSaveSlotInfo info)
    {
        DateTime savedAt = ParseTimestamp(info.SavedAtUtc).ToLocalTime();
        string time = savedAt == DateTime.MinValue ? "시간 정보 없음" : savedAt.ToString("M월 d일 HH:mm", CultureInfo.CurrentCulture);
        string debugBadge = info.DebugModified ? " · 디버그 사용" : string.Empty;
        string survival = DungeonSurvivalPressureRules.GetDisplayName(
            DungeonSurvivalPressureRules.Normalize(
                info.SurvivalPressureValue));
        return $"{time}\n{info.Day}일차 · {info.Money:N0} 골드 · 생존 {survival}{debugBadge}";
    }

    private static DateTime ParseTimestamp(string value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed)
            ? parsed
            : DateTime.MinValue;
    }

    private void CapturePauseState()
    {
        if (pauseWasCaptured)
        {
            return;
        }

        wasPausedBeforeModal = gameSpeedController.IsPaused;
        pauseWasCaptured = true;
    }

    private void RestorePauseState()
    {
        if (!pauseWasCaptured)
        {
            return;
        }

        if (wasPausedBeforeModal)
        {
            gameSpeedController.SetPaused(true);
        }
        else if (ownerRunManagerProvider.TryGetManager(out OwnerRunManager ownerManager)
            && ownerManager.CurrentOwnerActor == null)
        {
            gameSpeedController.SetPaused(true);
        }
        else
        {
            gameSpeedController.SetPaused(false);
        }

        pauseWasCaptured = false;
    }

    private void SetPaused(bool value)
    {
        gameSpeedController.SetPaused(value);
    }

    private void SetStatus(string message, bool error)
    {
        statusText.text = message ?? string.Empty;
        statusText.color = error ? DungeonUiTheme.Danger : DungeonUiTheme.TextSecondary;
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
        bool selected = false,
        bool destructive = false)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Button button = buttonObject.GetComponent<Button>();
        if (action != null)
        {
            button.onClick.AddListener(action);
        }

        CreateText(buttonObject.transform, "Label", label, 17f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
        DungeonUiTheme.StyleButton(button, selected, destructive);
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
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = alignment;
        text.characterSpacing = 0f;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(12f, fontSize - 5f);
        text.fontSizeMax = fontSize;
        return text;
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

    private sealed class SlotRow
    {
        public SlotRow(TMP_Text metadata, Button save, Button load, Button delete)
        {
            Metadata = metadata;
            Save = save;
            Load = load;
            Delete = delete;
        }

        public TMP_Text Metadata { get; }
        public Button Save { get; }
        public Button Load { get; }
        public Button Delete { get; }
    }
}
