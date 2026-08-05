using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;
using DungeonStory.Foundation;

public sealed class CombatCommandBarUiController :
    IStartable,
    ITickable,
    IDisposable
{
    private const int SortingOrder = 860;

    private readonly IDungeonUiCanvasProvider canvasProvider;
    private readonly ITmpKoreanFontService fontService;
    private readonly IPlayerCombatCommandSource commands;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IUiClock uiClock;
    private readonly IDungeonUserSettingsService userSettings;
    private GameObject root;
    private RectTransform panel;
    private TMP_Text status;
    private readonly Dictionary<CombatCommandType, Button> modeButtons =
        new Dictionary<CombatCommandType, Button>();
    private Button stanceButton;
    private Button holdFireButton;
    private Button fireModeButton;
    private float nextRefreshAt;
    private CombatFireMode requestedMode = CombatFireMode.Aimed;
    private bool requestedHoldFire;

    public CombatCommandBarUiController(
        IDungeonUiCanvasProvider canvasProvider,
        ITmpKoreanFontService fontService,
        IPlayerCombatCommandSource commands,
        ICombatEquipmentRuntime equipment,
        IUiClock uiClock,
        IDungeonUserSettingsService userSettings)
    {
        this.canvasProvider = canvasProvider ?? throw new ArgumentNullException(nameof(canvasProvider));
        this.fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.userSettings = userSettings
            ?? throw new ArgumentNullException(nameof(userSettings));
    }

    public void Start()
    {
        CreateUi(canvasProvider.GetOrCreateCanvas());
        Refresh(force: true);
    }

    public void Tick()
    {
        if (uiClock.Time >= nextRefreshAt)
        {
            nextRefreshAt = uiClock.Time + 0.1f;
            Refresh(force: false);
        }
    }

    public void Dispose()
    {
        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
        }
    }

    private void CreateUi(Canvas parent)
    {
        root = new GameObject(
            "CombatCommandRuntimeUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster));
        root.transform.SetParent(parent.transform, false);
        Stretch(root.GetComponent<RectTransform>());
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;

        GameObject panelObject = new GameObject(
            "CombatCommandBar",
            typeof(RectTransform),
            typeof(Image),
            typeof(HorizontalLayoutGroup));
        panelObject.transform.SetParent(root.transform, false);
        panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 62f);
        panel.sizeDelta = new Vector2(1050f, 62f);
        panelObject.GetComponent<Image>().color = DungeonUiTheme.Panel;
        HorizontalLayoutGroup layout = panelObject.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;

        stanceButton = CreateButton(panel, "CombatStanceButton", "전투 태세", ToggleStance, 96f);
        CreateModeButton(CombatCommandType.Move, "이동");
        CreateModeButton(CombatCommandType.Attack, "공격");
        CreateModeButton(CombatCommandType.ForceFire, "강제 사격", destructive: true);
        CreateModeButton(CombatCommandType.MoveToCover, "엄폐");
        CreateModeButton(CombatCommandType.Rescue, "구조");
        CreateButton(panel, "CombatSwitchWeaponButton", "무기 교체", SwitchWeapon, 92f);
        CreateButton(panel, "CombatReloadButton", "재장전", Reload, 82f);
        fireModeButton = CreateButton(panel, "CombatFireModeButton", "조준", CycleFireMode, 76f);
        holdFireButton = CreateButton(panel, "CombatHoldFireButton", "사격 허용", ToggleHoldFire, 90f);

        status = CreateText(panel, "CombatCommandStatus", "캐릭터를 선택하세요.", 14f);
        LayoutElement statusLayout = status.gameObject.AddComponent<LayoutElement>();
        statusLayout.preferredWidth = 180f;
        statusLayout.flexibleWidth = 1f;
        status.alignment = TextAlignmentOptions.MidlineLeft;
        status.color = DungeonUiTheme.TextSecondary;
        DungeonUiThemeRuntime.Ensure(
            parent,
            fontService,
            uiClock,
            userSettings).ApplyNow();
    }

    private void CreateModeButton(
        CombatCommandType mode,
        string label,
        bool destructive = false)
    {
        Button button = CreateButton(
            panel,
            $"CombatMode_{mode}",
            label,
            () => SelectMode(mode),
            mode == CombatCommandType.ForceFire ? 96f : 72f);
        modeButtons[mode] = button;
        DungeonUiTheme.StyleButton(button, destructive: destructive);
    }

    private void SelectMode(CombatCommandType mode)
    {
        if (!commands.HasCombatStanceSelection)
        {
            SetStatus("먼저 전투 태세를 켜세요.", error: true);
            return;
        }

        commands.SetCombatInputMode(
            commands.CombatInputMode == mode ? CombatCommandType.None : mode);
        SetStatus(commands.CombatInputMode == CombatCommandType.None
            ? "명령 선택 취소"
            : GetModePrompt(mode));
        RefreshStyles();
    }

    private void ToggleStance()
    {
        if (!commands.ToggleSelectedCombatStance(out string message))
        {
            SetStatus(message, error: true);
            return;
        }

        SetStatus(message);
        Refresh(force: true);
    }

    private void SwitchWeapon()
    {
        Execute(
            () => commands.TrySwitchSelectedWeapons(out string message)
                ? (true, message)
                : (false, message));
    }

    private void Reload()
    {
        Execute(
            () => commands.TryReloadSelected(out string message)
                ? (true, message)
                : (false, message));
    }

    private void CycleFireMode()
    {
        requestedMode = requestedMode switch
        {
            CombatFireMode.Aimed => CombatFireMode.Rapid,
            CombatFireMode.Rapid => CombatFireMode.Suppressive,
            _ => CombatFireMode.Aimed
        };
        Execute(
            () => commands.TrySetSelectedFireMode(requestedMode, out string message)
                ? (true, message)
                : (false, message));
    }

    private void ToggleHoldFire()
    {
        requestedHoldFire = !requestedHoldFire;
        Execute(
            () => commands.TrySetSelectedHoldFire(requestedHoldFire, out string message)
                ? (true, message)
                : (false, message));
    }

    private void Execute(Func<(bool success, string message)> action)
    {
        (bool success, string message) = action();
        SetStatus(message, !success);
        Refresh(force: true);
    }

    private void Refresh(bool force)
    {
        bool hasSelection = commands.SelectedActors != null
            && commands.SelectedActors.Count > 0;
        if (root != null)
        {
            root.SetActive(hasSelection);
        }

        if (!hasSelection)
        {
            return;
        }

        CharacterActor selected = commands.SelectedActor;
        if (selected != null)
        {
            CharacterCombatLoadoutProfile profile = equipment.GetActiveProfileSnapshot(
                selected.Identity?.PersistentId ?? string.Empty);
            if (profile != null)
            {
                requestedMode = profile.fireMode;
                requestedHoldFire = profile.holdFire;
            }
        }

        if (fireModeButton != null)
        {
            SetButtonText(fireModeButton, requestedMode switch
            {
                CombatFireMode.Rapid => "속사",
                CombatFireMode.Suppressive => "제압",
                _ => "조준"
            });
        }

        if (holdFireButton != null)
        {
            SetButtonText(holdFireButton, requestedHoldFire ? "사격 중지" : "사격 허용");
        }

        RefreshStyles();
    }

    private void RefreshStyles()
    {
        DungeonUiTheme.StyleButton(stanceButton, selected: commands.HasCombatStanceSelection);
        foreach (KeyValuePair<CombatCommandType, Button> pair in modeButtons)
        {
            DungeonUiTheme.StyleButton(
                pair.Value,
                selected: commands.CombatInputMode == pair.Key,
                destructive: pair.Key == CombatCommandType.ForceFire);
        }

        DungeonUiTheme.StyleButton(holdFireButton, selected: requestedHoldFire);
        DungeonUiTheme.StyleButton(fireModeButton, selected: true);
    }

    private void SetStatus(string message, bool error = false)
    {
        if (status == null)
        {
            return;
        }

        status.text = message ?? string.Empty;
        status.color = error ? DungeonUiTheme.Danger : DungeonUiTheme.TextSecondary;
    }

    private Button CreateButton(
        Transform parent,
        string name,
        string label,
        Action callback,
        float width)
    {
        GameObject gameObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        gameObject.transform.SetParent(parent, false);
        LayoutElement layout = gameObject.GetComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = width;
        Button button = gameObject.GetComponent<Button>();
        button.onClick.AddListener(() => callback?.Invoke());
        TMP_Text text = CreateText(gameObject.transform, "Label", label, 14f);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.Center;
        DungeonUiTheme.StyleButton(button);
        return button;
    }

    private TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        float size)
    {
        GameObject gameObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        gameObject.transform.SetParent(parent, false);
        TMP_Text text = gameObject.GetComponent<TMP_Text>();
        text.font = fontService.Resolve();
        text.text = value ?? string.Empty;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.characterSpacing = 0f;
        return text;
    }

    private static void SetButtonText(Button button, string value)
    {
        TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    private static string GetModePrompt(CombatCommandType mode)
    {
        return mode switch
        {
            CombatCommandType.Move => "이동할 칸을 우클릭하세요.",
            CombatCommandType.Attack => "공격할 적을 우클릭하세요.",
            CombatCommandType.ForceFire => "위험: 대상 또는 칸을 우클릭하세요.",
            CombatCommandType.MoveToCover => "이동할 엄폐물을 우클릭하세요.",
            CombatCommandType.Rescue => "쓰러진 캐릭터를 우클릭하세요.",
            _ => "명령 대상을 선택하세요."
        };
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
