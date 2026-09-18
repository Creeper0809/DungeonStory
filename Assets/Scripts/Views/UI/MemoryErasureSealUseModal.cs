using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents the player-only selection and explicit confirmation flow for a
/// memory-erasure seal. The command service remains the sole mutation owner.
/// </summary>
public sealed class MemoryErasureSealUseModal : IDisposable
{
    private readonly Transform uiHost;
    private readonly ITmpKoreanFontService fontService;
    private readonly IMemoryErasureSealCommandService commands;
    private readonly ICharacterWorldQuery characters;

    private GameObject root;
    private TMP_Text titleText;
    private TMP_Text instructionText;
    private TMP_Text statusText;
    private RectTransform choiceContent;
    private Button backButton;
    private Button confirmButton;
    private CharacterActor selectedTarget;
    private MemoryErasureSealTraitTarget selectedTrait;
    private ViewState viewState;

    public MemoryErasureSealUseModal(
        Transform uiHost,
        ITmpKoreanFontService fontService,
        IMemoryErasureSealCommandService commands,
        ICharacterWorldQuery characters)
    {
        this.uiHost = uiHost ?? throw new ArgumentNullException(nameof(uiHost));
        this.fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        commands.OrderChanged += OnOrderChanged;
        commands.UseCompleted += OnUseCompleted;
    }

    public void Open()
    {
        EnsureView();
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        selectedTarget = null;
        selectedTrait = null;
        ShowTargetSelection();
    }

    public void Close()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    public void Dispose()
    {
        commands.OrderChanged -= OnOrderChanged;
        commands.UseCompleted -= OnUseCompleted;
        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
            root = null;
        }
    }

    private void EnsureView()
    {
        if (root != null)
        {
            return;
        }

        root = new GameObject(
            "MemoryErasureSealUseModal",
            typeof(RectTransform),
            typeof(Image));
        root.transform.SetParent(uiHost, false);
        Stretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);

        GameObject panel = RuntimePanelFactoryUtility.CreatePanel(
            root.transform,
            "MemoryErasureSealUsePanel",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(760f, 680f));

        RectTransform header = CreateRect("Header", panel.transform);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = Vector2.zero;
        header.sizeDelta = new Vector2(0f, 66f);
        header.gameObject.AddComponent<Image>().color = DungeonUiTheme.SurfaceRaised;

        titleText = CreateText(
            "Title",
            header,
            23f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        Stretch(titleText.rectTransform, new Vector2(20f, 0f), new Vector2(-92f, 0f));

        Button close = CreateButton("Close", header, "닫기", Close);
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
        closeRect.pivot = Vector2.one;
        closeRect.anchoredPosition = new Vector2(-14f, -15f);
        closeRect.sizeDelta = new Vector2(66f, 36f);

        instructionText = CreateText(
            "Instruction",
            panel.transform,
            17f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        Stretch(instructionText.rectTransform, new Vector2(22f, 550f), new Vector2(-22f, -84f));
        instructionText.color = DungeonUiTheme.TextPrimary;

        statusText = CreateText(
            "Status",
            panel.transform,
            15f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        Stretch(statusText.rectTransform, new Vector2(22f, 504f), new Vector2(-22f, -138f));
        statusText.color = DungeonUiTheme.TextSecondary;

        RectTransform viewport = CreateRect("ChoicesViewport", panel.transform);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(22f, 64f);
        viewport.offsetMax = new Vector2(-22f, -184f);
        viewport.gameObject.AddComponent<Image>().color = DungeonUiTheme.Panel;
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        scroll.viewport = viewport;

        choiceContent = CreateRect("Choices", viewport);
        choiceContent.anchorMin = new Vector2(0f, 1f);
        choiceContent.anchorMax = new Vector2(1f, 1f);
        choiceContent.pivot = new Vector2(0.5f, 1f);
        choiceContent.anchoredPosition = Vector2.zero;
        choiceContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup choicesLayout = choiceContent.gameObject.AddComponent<VerticalLayoutGroup>();
        choicesLayout.padding = new RectOffset(10, 10, 10, 10);
        choicesLayout.spacing = 8f;
        choicesLayout.childAlignment = TextAnchor.UpperLeft;
        choicesLayout.childControlWidth = true;
        choicesLayout.childControlHeight = true;
        choicesLayout.childForceExpandWidth = true;
        choicesLayout.childForceExpandHeight = false;
        ContentSizeFitter choicesFitter = choiceContent.gameObject.AddComponent<ContentSizeFitter>();
        choicesFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        choicesFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = choiceContent;

        RectTransform footer = CreateRect("Footer", panel.transform);
        footer.anchorMin = new Vector2(0f, 0f);
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
        footer.anchoredPosition = new Vector2(0f, 12f);
        footer.sizeDelta = new Vector2(0f, 42f);

        backButton = CreateButton("Back", footer, "이전", ShowPrevious);
        RectTransform backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 0.5f);
        backRect.anchoredPosition = Vector2.zero;
        backRect.sizeDelta = new Vector2(116f, 0f);

        confirmButton = CreateButton("MemoryErasureSealConfirm", footer, "소거 확정", Confirm);
        RectTransform confirmRect = confirmButton.GetComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(1f, 0f);
        confirmRect.anchorMax = new Vector2(1f, 1f);
        confirmRect.pivot = new Vector2(1f, 0.5f);
        confirmRect.anchoredPosition = Vector2.zero;
        confirmRect.sizeDelta = new Vector2(150f, 0f);

        root.SetActive(false);
    }

    private void ShowTargetSelection()
    {
        viewState = ViewState.TargetSelection;
        selectedTarget = null;
        selectedTrait = null;
        titleText.text = "기억 소거 인장 사용";
        instructionText.text = "후천 특성을 지닌 살아 있는 대상을 선택하세요.";
        statusText.text = "인장은 1개만 사용되며, 다음 단계에서 소거를 확정합니다.";
        SetFooter(showBack: false, showConfirm: false);
        ClearChoices();

        IReadOnlyList<CharacterActor> candidates = characters.Characters
            .Where(IsEligibleTarget)
            .Where(actor => commands.GetActiveTraitTargets(actor).Count > 0)
            .OrderBy(actor => actor.Identity?.DisplayName ?? actor.name, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Count == 0)
        {
            AddMessage("소거할 활성 후천 특성이 있는 살아 있는 대상이 없습니다.");
            return;
        }

        foreach (CharacterActor actor in candidates)
        {
            IReadOnlyList<MemoryErasureSealTraitTarget> traits =
                commands.GetActiveTraitTargets(actor);
            string label = $"{GetDisplayName(actor)}\n활성 후천 특성 {traits.Count}개";
            if (commands.TryGetActiveOrder(actor, out MemoryErasureSealOrderSnapshot order))
            {
                label += $"\n진행 중 · {FormatOrderStage(order.Stage)}";
            }

            CreateChoiceButton(
                "MemoryErasureSealTarget_" + GetTargetId(actor),
                label,
                () => SelectTarget(actor));
        }
    }

    private void SelectTarget(CharacterActor actor)
    {
        selectedTarget = actor ?? throw new ArgumentNullException(nameof(actor));
        selectedTrait = null;
        if (commands.TryGetActiveOrder(selectedTarget, out MemoryErasureSealOrderSnapshot order))
        {
            ShowOrderProgress(order);
            return;
        }

        ShowTraitSelection();
    }

    private void ShowTraitSelection()
    {
        if (selectedTarget == null)
        {
            ShowTargetSelection();
            return;
        }

        IReadOnlyList<MemoryErasureSealTraitTarget> traits =
            commands.GetActiveTraitTargets(selectedTarget);
        viewState = ViewState.TraitSelection;
        titleText.text = "소거할 후천 특성 선택";
        instructionText.text = GetDisplayName(selectedTarget) + "의 활성 후천 특성 중 하나를 선택하세요.";
        statusText.text = "선택만으로는 특성이나 인장이 변경되지 않습니다.";
        SetFooter(showBack: true, showConfirm: false);
        ClearChoices();
        if (traits.Count == 0)
        {
            AddMessage("이 대상에게는 소거할 활성 후천 특성이 없습니다.");
            return;
        }

        foreach (MemoryErasureSealTraitTarget trait in traits)
        {
            string modules = trait.ModuleIds.Count == 0
                ? string.Empty
                : "\n구성: " + string.Join(", ", trait.ModuleIds);
            CreateChoiceButton(
                "MemoryErasureSealTrait_" + trait.InstanceId,
                $"{trait.DisplayName}\n{trait.Description}{modules}",
                () => SelectTrait(trait));
        }
    }

    private void SelectTrait(MemoryErasureSealTraitTarget trait)
    {
        selectedTrait = trait ?? throw new ArgumentNullException(nameof(trait));
        ShowConfirmation();
    }

    private void ShowConfirmation(string status = null)
    {
        if (selectedTarget == null || selectedTrait == null)
        {
            ShowTargetSelection();
            return;
        }

        if (commands.TryGetActiveOrder(selectedTarget, out MemoryErasureSealOrderSnapshot order))
        {
            ShowOrderProgress(order);
            return;
        }

        viewState = ViewState.Confirmation;
        titleText.text = "기억 소거 확인";
        instructionText.text = "아래 내용을 확인한 뒤에만 소거 명령을 발행합니다.";
        statusText.text = string.IsNullOrWhiteSpace(status)
            ? "확정 전에는 인장 1개와 후천 특성 모두 변경되지 않습니다."
            : status;
        SetFooter(showBack: true, showConfirm: true);
        ClearChoices();
        AddMessage(
            $"대상: {GetDisplayName(selectedTarget)}\n"
            + $"후천 특성: {selectedTrait.DisplayName}\n"
            + $"설명: {selectedTrait.Description}\n"
            + "소비: 기억 소거 인장 1개\n\n"
            + "소거된 후천 특성은 되돌릴 수 없습니다.");
    }

    private void Confirm()
    {
        if (selectedTarget == null || selectedTrait == null)
        {
            throw new InvalidOperationException(
                "Memory-erasure seal confirmation requires a target and trait selection.");
        }

        MemoryErasureSealCommandResult result = commands.TryIssueConfirmedUse(
            MemoryErasureSealItemRules.ItemId,
            selectedTarget,
            selectedTrait.InstanceId);
        if (!result.Accepted)
        {
            ShowConfirmation(FormatCommandResult(result));
            return;
        }

        if (commands.TryGetActiveOrder(selectedTarget, out MemoryErasureSealOrderSnapshot order))
        {
            ShowOrderProgress(order);
            return;
        }

        if (commands.TryGetLatestResult(selectedTarget, out MemoryErasureSealUseResult completed)
            && string.Equals(completed.OperationId, result.OperationId, StringComparison.Ordinal))
        {
            ShowCompletion(completed);
            return;
        }

        ShowAcceptedAwaiting(result);
    }

    private void ShowAcceptedAwaiting(MemoryErasureSealCommandResult result)
    {
        viewState = ViewState.Progress;
        titleText.text = "기억 소거 명령 수락";
        instructionText.text = "명령이 수락되었습니다. 진행 상태를 기다리는 동안 같은 요청을 다시 발행할 수 없습니다.";
        statusText.text = FormatCommandResult(result);
        SetFooter(showBack: false, showConfirm: false);
        ClearChoices();
        AddMessage(
            $"대상: {GetDisplayName(selectedTarget)}\n"
            + $"후천 특성: {selectedTrait?.DisplayName ?? result.TraitInstanceId}\n"
            + "서비스가 진행 또는 완료 결과를 발행하면 이 창에 표시됩니다.");
    }

    private void ShowOrderProgress(MemoryErasureSealOrderSnapshot order)
    {
        viewState = ViewState.Progress;
        titleText.text = "기억 소거 진행 중";
        instructionText.text = "명령이 수락되었습니다. 물리 인장과 특성 변경은 서비스의 진행 상태에 따라 처리됩니다.";
        statusText.text = FormatOrder(order);
        SetFooter(showBack: false, showConfirm: false);
        ClearChoices();
        AddMessage(
            $"대상: {GetDisplayName(selectedTarget)}\n"
            + $"후천 특성: {selectedTrait?.DisplayName ?? order.TraitInstanceId}\n"
            + "진행 상태가 갱신되면 이 창에 표시됩니다.");
    }

    private void ShowCompletion(MemoryErasureSealUseResult result)
    {
        viewState = ViewState.Completion;
        titleText.text = "기억 소거 결과";
        instructionText.text = result.Succeeded
            ? "기억 소거 처리 결과가 확정되었습니다."
            : "기억 소거 처리가 완료되지 않았습니다.";
        statusText.text = FormatUseResult(result);
        SetFooter(showBack: false, showConfirm: false);
        ClearChoices();
        AddMessage(
            $"대상: {GetDisplayName(selectedTarget)}\n"
            + $"후천 특성: {selectedTrait?.DisplayName ?? result.TraitInstanceId}\n"
            + $"작업 ID: {result.OperationId}\n"
            + $"감사 ID: {result.AuditId}");
    }

    private void ShowPrevious()
    {
        if (viewState == ViewState.TraitSelection || selectedTarget == null)
        {
            ShowTargetSelection();
            return;
        }

        if (viewState == ViewState.Confirmation)
        {
            selectedTrait = null;
            ShowTraitSelection();
        }
    }

    private void OnOrderChanged(MemoryErasureSealOrderSnapshot order)
    {
        if (!IsVisibleFor(order.TargetCharacterId))
        {
            return;
        }

        ShowOrderProgress(order);
    }

    private void OnUseCompleted(MemoryErasureSealUseResult result)
    {
        if (!IsVisibleFor(result.TargetCharacterId))
        {
            return;
        }

        ShowCompletion(result);
    }

    private bool IsVisibleFor(string targetCharacterId)
    {
        return root != null
            && root.activeSelf
            && selectedTarget != null
            && string.Equals(
                GetTargetId(selectedTarget),
                targetCharacterId,
                StringComparison.Ordinal);
    }

    private void SetFooter(bool showBack, bool showConfirm)
    {
        backButton.gameObject.SetActive(showBack);
        confirmButton.gameObject.SetActive(showConfirm);
        confirmButton.interactable = showConfirm;
    }

    private void ClearChoices()
    {
        for (int index = choiceContent.childCount - 1; index >= 0; index--)
        {
            UnityEngine.Object.Destroy(choiceContent.GetChild(index).gameObject);
        }
    }

    private void CreateChoiceButton(string name, string label, Action action)
    {
        Button button = CreateButton(name, choiceContent, label, action);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 62f;
        layout.preferredHeight = 76f;
        TMP_Text text = button.transform.Find("Label")?.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.margin = new Vector4(12f, 5f, 12f, 5f);
        }
    }

    private void AddMessage(string message)
    {
        TMP_Text text = CreateText(
            "Message",
            choiceContent,
            16f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        text.text = message;
        text.color = DungeonUiTheme.TextSecondary;
        text.margin = new Vector4(12f, 10f, 12f, 10f);
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
        float height = Mathf.Max(84f, text.preferredHeight + 20f);
        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    private static bool IsEligibleTarget(CharacterActor actor) => actor != null
        && !actor.IsDead
        && !string.IsNullOrWhiteSpace(GetTargetId(actor));

    private static string GetTargetId(CharacterActor actor) =>
        actor?.Identity?.PersistentId?.Trim() ?? string.Empty;

    private static string GetDisplayName(CharacterActor actor) => actor == null
        ? "선택 없음"
        : actor.Identity?.DisplayName ?? actor.name;

    private static string FormatCommandResult(MemoryErasureSealCommandResult result)
    {
        string status = result.Status switch
        {
            MemoryErasureSealCommandStatus.Accepted => "명령 수락",
            MemoryErasureSealCommandStatus.AlreadyCompleted => "이미 완료됨",
            MemoryErasureSealCommandStatus.InvalidItem => "인장 아이템이 올바르지 않음",
            MemoryErasureSealCommandStatus.InvalidTarget => "대상이 올바르지 않음",
            MemoryErasureSealCommandStatus.TraitUnavailable => "선택한 후천 특성을 사용할 수 없음",
            MemoryErasureSealCommandStatus.OrderAlreadyActive => "이미 진행 중인 소거 명령이 있음",
            MemoryErasureSealCommandStatus.CharacterUnavailable => "대상이 현재 작업을 시작할 수 없음",
            MemoryErasureSealCommandStatus.WarehouseSealUnavailable => "저장소에서 인장을 확보할 수 없음",
            MemoryErasureSealCommandStatus.ActionOwnershipUnavailable => "대상 행동 권한을 확보할 수 없음",
            _ => result.Status.ToString()
        };
        return string.IsNullOrWhiteSpace(result.Detail)
            ? status
            : status + " · " + result.Detail;
    }

    private static string FormatOrder(MemoryErasureSealOrderSnapshot order)
    {
        string status = "진행 단계: " + FormatOrderStage(order.Stage);
        return string.IsNullOrWhiteSpace(order.Detail)
            ? status
            : status + " · " + order.Detail;
    }

    private static string FormatOrderStage(MemoryErasureSealOrderStage stage) => stage switch
    {
        MemoryErasureSealOrderStage.Reserved => "인장 예약",
        MemoryErasureSealOrderStage.MovingToWarehouse => "저장소로 이동",
        MemoryErasureSealOrderStage.Carrying => "인장 운반",
        MemoryErasureSealOrderStage.SuspendedAfterPickup => "픽업 후 중단",
        MemoryErasureSealOrderStage.Applying => "특성 소거 적용",
        MemoryErasureSealOrderStage.RecoveryPending => "복구 대기",
        _ => "상태 없음"
    };

    private static string FormatUseResult(MemoryErasureSealUseResult result)
    {
        string status = result.Status switch
        {
            MemoryErasureSealUseStatus.Succeeded => "소거 완료",
            MemoryErasureSealUseStatus.AlreadyCompleted => "이미 완료됨",
            MemoryErasureSealUseStatus.InterruptedBeforePickup => "픽업 전 중단",
            MemoryErasureSealUseStatus.InterruptedAfterPickup => "픽업 후 중단",
            MemoryErasureSealUseStatus.TargetChanged => "대상 상태 변경으로 중단",
            MemoryErasureSealUseStatus.PhysicalCommitFailed => "인장 물리 처리 실패",
            MemoryErasureSealUseStatus.TraitCommitFailed => "특성 소거 처리 실패",
            MemoryErasureSealUseStatus.RecoveryFailed => "복구 실패",
            MemoryErasureSealUseStatus.InvalidRequest => "유효하지 않은 소거 요청",
            _ => result.Status.ToString()
        };
        return string.IsNullOrWhiteSpace(result.Detail)
            ? status
            : status + " · " + result.Detail;
    }

    private Button CreateButton(string name, Transform parent, string label, Action action)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() => action());
        DungeonUiTheme.StyleButton(button, false);
        TMP_Text text = CreateText(
            "Label",
            buttonObject.transform,
            16f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
        text.text = label;
        return button;
    }

    private TMP_Text CreateText(
        string name,
        Transform parent,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        fontService.Apply(text);
        text.fontSize = fontSize;
        text.fontSizeMin = Mathf.Max(11f, fontSize - 5f);
        text.fontSizeMax = fontSize;
        text.enableAutoSizing = true;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = DungeonUiTheme.TextPrimary;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private enum ViewState
    {
        TargetSelection,
        TraitSelection,
        Confirmation,
        Progress,
        Completion
    }
}
