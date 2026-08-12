using System;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns popup lifecycle, generated-view setup, and the detailed-stat overlay.
/// </summary>
public sealed class CharacterSummaryShellPresenter
{
    private readonly IUiPopupService popupService;
    private readonly ICharacterSummaryRuntimeLogFactory viewFactory;
    private readonly ICharacterDetailedStatsRuntime detailedStatsRuntime;
    private readonly ICareerMentorshipService mentorshipService;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly ICharacterNarrativeCatalog narrativeCatalog;
    private readonly IGameEventBus gameEventBus;
    private GameObject detailedStatsPanel;
    private TMP_Text detailedStatsTitle;
    private TMP_Text detailedStatsText;
    private Button[] detailedStatsTabButtons = Array.Empty<Button>();
    private CharacterDetailedStatsTab selectedDetailedStatsTab;
    private Button mentorshipManageButton;
    private GameObject mentorshipPanel;
    private TMP_Text mentorshipStatusText;
    private Button mentorCycleButton;
    private Button studentCycleButton;
    private Button proficiencyCycleButton;
    private CharacterActor detailedActor;
    private CharacterActor[] mentorshipCharacters = Array.Empty<CharacterActor>();
    private ProficiencyDefinitionSO[] mentorshipProficiencies =
        Array.Empty<ProficiencyDefinitionSO>();
    private int mentorIndex;
    private int studentIndex;
    private int proficiencyIndex;
    private CharacterId pendingMentorId;
    private CharacterProficiencyId pendingProficiencyId;
    private BuildingInstanceId pendingAcademyId;
    private Graphic[] detailedBackgroundGraphics = Array.Empty<Graphic>();
    private bool[] detailedBackgroundRaycastStates = Array.Empty<bool>();

    public CharacterSummaryShellPresenter(
        IUiPopupService popupService,
        ICharacterSummaryRuntimeLogFactory viewFactory,
        ICharacterDetailedStatsRuntime detailedStatsRuntime,
        ICareerMentorshipService mentorshipService,
        ICharacterWorldQuery characterWorld,
        IBuildingWorldQuery buildingWorld,
        ICharacterNarrativeCatalog narrativeCatalog,
        IGameEventBus gameEventBus)
    {
        this.popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        this.viewFactory = viewFactory ?? throw new ArgumentNullException(nameof(viewFactory));
        this.detailedStatsRuntime = detailedStatsRuntime
            ?? throw new ArgumentNullException(nameof(detailedStatsRuntime));
        this.mentorshipService = mentorshipService
            ?? throw new ArgumentNullException(nameof(mentorshipService));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.narrativeCatalog = narrativeCatalog
            ?? throw new ArgumentNullException(nameof(narrativeCatalog));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public void Initialize(
        ICharacterSummaryGeneratedView view,
        CharacterSummaryViewActions actions,
        GameObject uiRoot)
    {
        EnsureView(view, actions, uiRoot);
        uiRoot.SetActive(false);
    }

    public void Open(
        UIPopUp popup,
        ICharacterSummaryGeneratedView view,
        CharacterSummaryViewActions actions,
        GameObject uiRoot)
    {
        if (popup == null)
        {
            throw new ArgumentNullException(nameof(popup));
        }
        popupService.CloseAll();
        EnsureView(view, actions, uiRoot);
        uiRoot.SetActive(true);
        popupService.Open(popup);
        CloseDetailedStats();
    }

    public void RequestClose(UIPopUp popup)
    {
        if (popup != null)
        {
            popupService.ClosePeek(popup);
        }
    }

    public void BindDetailedStats(
        Button entryButton,
        GameObject panel,
        TMP_Text title,
        TMP_Text content,
        Button[] tabButtons)
    {
        if (entryButton != null)
        {
            entryButton.interactable = true;
        }
        detailedStatsPanel = panel;
        detailedStatsTitle = title;
        detailedStatsText = content;
        detailedStatsTabButtons = tabButtons ?? Array.Empty<Button>();
        EnsureMentorshipControls(panel);
        CloseDetailedStats();
    }

    public void OpenDetailedStats(CharacterActor actor)
    {
        if (actor == null || detailedStatsPanel == null)
        {
            return;
        }

        selectedDetailedStatsTab = CharacterDetailedStatsTab.Summary;
        detailedActor = actor;
        detailedStatsPanel.transform.SetAsLastSibling();
        SetDetailedBackgroundRaycasts(false);
        detailedStatsPanel.SetActive(true);
        RefreshGraphicRegistration(detailedStatsPanel);
        RefreshDetailedStats(actor);
    }

    public void CloseDetailedStats()
    {
        mentorshipPanel?.SetActive(false);
        detailedStatsPanel?.SetActive(false);
        SetDetailedBackgroundRaycasts(true);
    }

    private void SetDetailedBackgroundRaycasts(bool restore)
    {
        if (detailedStatsPanel == null || detailedStatsPanel.transform.parent == null)
        {
            return;
        }

        if (!restore)
        {
            Transform inputRoot = detailedStatsPanel.transform.parent;
            Canvas canvas = detailedStatsPanel.GetComponentInParent<Canvas>();
            Transform scanRoot = canvas != null ? canvas.transform : inputRoot;
            Graphic inputRouterGraphic = inputRoot.GetComponent<Graphic>();
            detailedBackgroundGraphics = scanRoot
                .GetComponentsInChildren<Graphic>(true)
                .Where(graphic => graphic != null
                    && graphic != inputRouterGraphic
                    && !graphic.transform.IsChildOf(detailedStatsPanel.transform))
                .ToArray();
            detailedBackgroundRaycastStates = detailedBackgroundGraphics
                .Select(graphic => graphic.raycastTarget)
                .ToArray();
            foreach (Graphic graphic in detailedBackgroundGraphics)
            {
                graphic.raycastTarget = false;
            }
            return;
        }

        for (int i = 0;
             i < detailedBackgroundGraphics.Length
             && i < detailedBackgroundRaycastStates.Length;
             i++)
        {
            if (detailedBackgroundGraphics[i] != null)
            {
                detailedBackgroundGraphics[i].raycastTarget =
                    detailedBackgroundRaycastStates[i];
            }
        }
        detailedBackgroundGraphics = Array.Empty<Graphic>();
        detailedBackgroundRaycastStates = Array.Empty<bool>();
    }

    public void ShowDetailedStatsTab(CharacterActor actor, CharacterDetailedStatsTab tab)
    {
        selectedDetailedStatsTab = tab;
        RefreshDetailedStats(actor);
    }

    public void RefreshDetailedStats(CharacterActor actor)
    {
        if (actor == null
            || detailedStatsPanel == null
            || !detailedStatsPanel.activeInHierarchy)
        {
            return;
        }

        CharacterDetailedStatsSnapshot snapshot = detailedStatsRuntime.GetSnapshot(actor);
        detailedActor = actor;
        if (detailedStatsTitle != null)
        {
            detailedStatsTitle.text =
                $"{snapshot.DisplayName} · {CharacterDetailedStatsRuntime.TabLabel(selectedDetailedStatsTab)}";
        }

        if (detailedStatsText != null)
        {
            StringBuilder builder = new StringBuilder(2048);
            foreach (CharacterDetailedStatRow row in snapshot.GetRows(selectedDetailedStatsTab))
            {
                builder.Append("<b>").Append(row.Label).Append("</b>  ")
                    .Append(row.Value).AppendLine();
                if (!string.IsNullOrWhiteSpace(row.Detail))
                {
                    builder.Append("<color=#B8B5AD>")
                        .Append(row.Detail)
                        .Append("</color>")
                        .AppendLine();
                }
                builder.AppendLine();
            }
            detailedStatsText.text = builder.Length > 0
                ? builder.ToString().TrimEnd()
                : CharacterSummaryUiTextQuery.Get(
                    "CharacterSummary.Detailed.Empty");
        }

        for (int i = 0; i < detailedStatsTabButtons.Length; i++)
        {
            DungeonUiTheme.StyleButton(
                detailedStatsTabButtons[i],
                selected: i == (int)selectedDetailedStatsTab);
        }

        if (mentorshipManageButton != null)
        {
            mentorshipManageButton.gameObject.SetActive(
                selectedDetailedStatsTab == CharacterDetailedStatsTab.Proficiencies);
        }

        if (mentorshipPanel != null && mentorshipPanel.activeSelf)
        {
            RefreshMentorshipPanel();
        }
    }

    public bool HandleCharacterSelection(CharacterActor selectedActor)
    {
        if (!pendingMentorId.IsValid || !pendingProficiencyId.IsValid)
        {
            return false;
        }

        bool hasStudent = selectedActor != null
            && CharacterPersistentIdentity.TryGet(
                selectedActor,
                out CharacterId studentId);
        string failureReason = string.Empty;
        bool assigned = hasStudent
            && mentorshipService.TryAssign(
                pendingMentorId,
                studentId,
                pendingAcademyId,
                pendingProficiencyId,
                out failureReason);
        if (!hasStudent)
        {
            failureReason = "학생으로 지정할 수 있는 주민을 선택해야 합니다.";
        }

        gameEventBus.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
            assigned ? "멘토 배정 완료" : "멘토 배정 실패",
            assigned
                ? "선택한 주민에게 멘토와 숙련 수업을 배정했습니다."
                : failureReason,
            assigned ? EventAlertImportance.Low : EventAlertImportance.Medium,
            "숙련")));
        pendingMentorId = default;
        pendingProficiencyId = default;
        pendingAcademyId = default;
        return true;
    }

    private void EnsureMentorshipControls(GameObject panel)
    {
        if (panel == null || mentorshipManageButton != null)
        {
            return;
        }

        Transform existing = panel.transform.Find("MentorshipManageButton");
        mentorshipManageButton = existing != null
            ? existing.GetComponent<Button>()
            : CreateButton(
                panel.transform,
                "MentorshipManageButton",
                "멘토·학생 관리");
        RectTransform manageRect = mentorshipManageButton.GetComponent<RectTransform>();
        manageRect.anchorMin = new Vector2(1f, 0f);
        manageRect.anchorMax = new Vector2(1f, 0f);
        manageRect.pivot = new Vector2(1f, 0f);
        manageRect.anchoredPosition = new Vector2(-14f, 14f);
        manageRect.sizeDelta = new Vector2(168f, 38f);
        mentorshipManageButton.onClick.RemoveAllListeners();
        mentorshipManageButton.onClick.AddListener(OpenMentorshipPanel);

        Transform viewport = panel.transform.Find("DetailedViewport");
        if (viewport is RectTransform viewportRect)
        {
            viewportRect.offsetMin = new Vector2(viewportRect.offsetMin.x, 62f);
        }

        mentorshipPanel = new GameObject(
            "MentorshipPanel",
            typeof(RectTransform),
            typeof(Image)).gameObject;
        mentorshipPanel.transform.SetParent(panel.transform, false);
        RectTransform modalRect = mentorshipPanel.GetComponent<RectTransform>();
        modalRect.anchorMin = new Vector2(0.5f, 0.5f);
        modalRect.anchorMax = new Vector2(0.5f, 0.5f);
        modalRect.pivot = new Vector2(0.5f, 0.5f);
        modalRect.sizeDelta = new Vector2(480f, 370f);
        mentorshipPanel.GetComponent<Image>().color = DungeonUiTheme.Panel;

        TMP_Text title = CreateText(
            mentorshipPanel.transform,
            "Title",
            "멘토·학생 배정",
            22f,
            FontStyles.Bold);
        Place(title.rectTransform, 18f, -18f, 444f, 34f);

        mentorCycleButton = CreateButton(
            mentorshipPanel.transform,
            "MentorCycle",
            "멘토");
        Place(mentorCycleButton.GetComponent<RectTransform>(), 18f, -66f, 444f, 42f);
        mentorCycleButton.onClick.AddListener(() => CycleMentorship(ref mentorIndex));

        studentCycleButton = CreateButton(
            mentorshipPanel.transform,
            "StudentCycle",
            "학생");
        Place(studentCycleButton.GetComponent<RectTransform>(), 18f, -114f, 444f, 42f);
        studentCycleButton.onClick.AddListener(() => CycleMentorship(ref studentIndex));

        proficiencyCycleButton = CreateButton(
            mentorshipPanel.transform,
            "ProficiencyCycle",
            "숙련");
        Place(proficiencyCycleButton.GetComponent<RectTransform>(), 18f, -162f, 444f, 42f);
        proficiencyCycleButton.onClick.AddListener(CycleProficiency);

        mentorshipStatusText = CreateText(
            mentorshipPanel.transform,
            "Status",
            string.Empty,
            15f,
            FontStyles.Normal);
        mentorshipStatusText.textWrappingMode = TextWrappingModes.Normal;
        Place(mentorshipStatusText.rectTransform, 18f, -212f, 444f, 60f);

        Button assign = CreateButton(mentorshipPanel.transform, "Assign", "배정");
        Place(assign.GetComponent<RectTransform>(), 18f, -286f, 102f, 42f);
        assign.onClick.AddListener(AssignSelectedMentorship);

        Button target = CreateButton(
            mentorshipPanel.transform,
            "TargetStudent",
            "대상 클릭 지정");
        Place(target.GetComponent<RectTransform>(), 126f, -286f, 126f, 42f);
        target.onClick.AddListener(StartMentorshipTargeting);

        Button clear = CreateButton(mentorshipPanel.transform, "Clear", "학생 배정 해제");
        Place(clear.GetComponent<RectTransform>(), 258f, -286f, 104f, 42f);
        clear.onClick.AddListener(ClearSelectedMentorship);

        Button close = CreateButton(mentorshipPanel.transform, "Close", "닫기");
        Place(close.GetComponent<RectTransform>(), 368f, -286f, 94f, 42f);
        close.onClick.AddListener(() => mentorshipPanel.SetActive(false));
        mentorshipPanel.SetActive(false);
    }

    private void OpenMentorshipPanel()
    {
        if (detailedActor == null || mentorshipPanel == null)
        {
            return;
        }

        mentorshipCharacters = (characterWorld.Characters
                ?? Array.Empty<CharacterActor>())
            .Where(value => value != null
                && !value.IsDead
                && CharacterPersistentIdentity.TryGet(value, out _))
            .OrderBy(GetDisplayName, StringComparer.Ordinal)
            .ToArray();
        mentorshipProficiencies = narrativeCatalog.Proficiencies
            .Where(value => value != null)
            .OrderBy(value => value.DisplayOrder)
            .ToArray();
        mentorIndex = Math.Max(
            0,
            Array.FindIndex(
                mentorshipCharacters,
                value => ReferenceEquals(value, detailedActor)));
        studentIndex = mentorshipCharacters.Length > 1
            ? (mentorIndex + 1) % mentorshipCharacters.Length
            : mentorIndex;
        proficiencyIndex = 0;
        mentorshipPanel.SetActive(true);
        mentorshipPanel.transform.SetAsLastSibling();
        RefreshGraphicRegistration(mentorshipPanel);
        RefreshMentorshipPanel();
    }

    private void CycleMentorship(ref int index)
    {
        if (mentorshipCharacters.Length > 0)
        {
            index = (index + 1) % mentorshipCharacters.Length;
        }
        RefreshMentorshipPanel();
    }

    private void CycleProficiency()
    {
        if (mentorshipProficiencies.Length > 0)
        {
            proficiencyIndex = (proficiencyIndex + 1)
                % mentorshipProficiencies.Length;
        }
        RefreshMentorshipPanel();
    }

    private void RefreshMentorshipPanel()
    {
        CharacterActor mentor = SelectedCharacter(mentorIndex);
        CharacterActor student = SelectedCharacter(studentIndex);
        ProficiencyDefinitionSO proficiency = SelectedProficiency();
        SetButtonLabel(
            mentorCycleButton,
            "멘토: " + GetDisplayName(mentor));
        SetButtonLabel(
            studentCycleButton,
            "학생: " + GetDisplayName(student));
        SetButtonLabel(
            proficiencyCycleButton,
            "숙련: " + (proficiency?.DisplayName ?? "선택 없음"));

        if (!TryGetSelection(
                out CharacterId mentorId,
                out CharacterId studentId,
                out BuildingInstanceId academyId,
                out CharacterProficiencyId proficiencyId))
        {
            mentorshipStatusText.text = "배정 가능한 주민, 숙련, 가동 중인 멘토 학원을 확인하세요.";
            return;
        }

        bool valid = mentorshipService.CanAssign(
            mentorId,
            studentId,
            academyId,
            proficiencyId,
            out string failureReason);
        mentorshipStatusText.text = valid
            ? "배정 가능 · 매일 멘토와 학생이 각각 30 작업량을 사용합니다."
            : failureReason;
    }

    private void AssignSelectedMentorship()
    {
        if (!TryGetSelection(
                out CharacterId mentorId,
                out CharacterId studentId,
                out BuildingInstanceId academyId,
                out CharacterProficiencyId proficiencyId))
        {
            RefreshMentorshipPanel();
            return;
        }

        bool assigned = mentorshipService.TryAssign(
            mentorId,
            studentId,
            academyId,
            proficiencyId,
            out string failureReason);
        mentorshipStatusText.text = assigned
            ? "멘토 수업을 배정했습니다."
            : failureReason;
        RefreshDetailedStats(detailedActor);
    }

    private void StartMentorshipTargeting()
    {
        CharacterActor mentor = SelectedCharacter(mentorIndex);
        ProficiencyDefinitionSO proficiency = SelectedProficiency();
        BuildingInstanceId academy = FindAcademyId();
        if (mentor == null
            || proficiency == null
            || !academy.IsValid
            || !CharacterPersistentIdentity.TryGet(mentor, out pendingMentorId))
        {
            mentorshipStatusText.text = "멘토, 숙련, 가동 중인 멘토 학원이 필요합니다.";
            return;
        }

        pendingProficiencyId = proficiency.ProficiencyId;
        pendingAcademyId = academy;
        mentorshipPanel.SetActive(false);
        gameEventBus.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
            "멘토 학생 선택",
            "이제 월드에서 학생으로 배정할 캐릭터를 클릭하세요.",
            EventAlertImportance.Low,
            "숙련")));
    }

    private void ClearSelectedMentorship()
    {
        CharacterActor student = SelectedCharacter(studentIndex);
        if (student != null
            && CharacterPersistentIdentity.TryGet(student, out CharacterId studentId))
        {
            mentorshipService.Clear(studentId);
            mentorshipStatusText.text = "학생의 기존 멘토 배정을 해제했습니다.";
            RefreshDetailedStats(detailedActor);
        }
    }

    private bool TryGetSelection(
        out CharacterId mentorId,
        out CharacterId studentId,
        out BuildingInstanceId academyId,
        out CharacterProficiencyId proficiencyId)
    {
        CharacterActor mentor = SelectedCharacter(mentorIndex);
        CharacterActor student = SelectedCharacter(studentIndex);
        ProficiencyDefinitionSO proficiency = SelectedProficiency();
        academyId = FindAcademyId();
        proficiencyId = proficiency?.ProficiencyId ?? default;
        mentorId = default;
        studentId = default;
        bool mentorValid = mentor != null
            && CharacterPersistentIdentity.TryGet(mentor, out mentorId);
        bool studentValid = student != null
            && CharacterPersistentIdentity.TryGet(student, out studentId);
        return mentorValid && studentValid && academyId.IsValid
            && proficiencyId.IsValid;
    }

    private BuildingInstanceId FindAcademyId() =>
        (buildingWorld.Buildings ?? Array.Empty<BuildableObject>())
            .Where(value => value != null
                && !value.isDestroy
                && !value.IsDamaged
                && value.BuildingData?.ResearchFacilityCommand ==
                    ResearchFacilityCommandKind.MentorAcademy)
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .Select(value => value.PersistentInstanceId)
            .FirstOrDefault();

    private CharacterActor SelectedCharacter(int index) =>
        index >= 0 && index < mentorshipCharacters.Length
            ? mentorshipCharacters[index]
            : null;

    private ProficiencyDefinitionSO SelectedProficiency() =>
        proficiencyIndex >= 0 && proficiencyIndex < mentorshipProficiencies.Length
            ? mentorshipProficiencies[proficiencyIndex]
            : null;

    private static string GetDisplayName(CharacterActor actor) =>
        actor == null
            ? "선택 없음"
            : actor.Identity?.DisplayName ?? actor.name;

    private static void RefreshGraphicRegistration(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        if (root.transform is RectTransform rootRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        }
        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null || !graphic.gameObject.activeInHierarchy)
            {
                continue;
            }
            bool wasEnabled = graphic.enabled;
            graphic.enabled = false;
            graphic.enabled = wasEnabled;
            graphic.SetAllDirty();
            if (wasEnabled && graphic.canvas != null)
            {
                GraphicRegistry.RegisterGraphicForCanvas(
                    graphic.canvas,
                    graphic);
                CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(
                    graphic);
            }
        }
        Canvas.ForceUpdateCanvases();
    }

    private Button CreateButton(Transform parent, string name, string label)
    {
        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        root.transform.SetParent(parent, false);
        Button button = root.GetComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        DungeonUiTheme.StyleButton(button);
        TMP_Text text = CreateText(
            root.transform,
            "Label",
            label,
            15f,
            FontStyles.Normal);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(6f, 2f);
        text.rectTransform.offsetMax = new Vector2(-6f, -2f);
        return button;
    }

    private TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        FontStyles style)
    {
        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        TMP_Text text = root.GetComponent<TMP_Text>();
        text.font = detailedStatsText?.font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.text = value;
        return text;
    }

    private static void Place(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetButtonLabel(Button button, string value)
    {
        TMP_Text text = button?.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = value;
        }
    }

    private void EnsureView(
        ICharacterSummaryGeneratedView view,
        CharacterSummaryViewActions actions,
        GameObject uiRoot)
    {
        if (view == null)
        {
            throw new ArgumentNullException(nameof(view));
        }
        if (actions == null)
        {
            throw new ArgumentNullException(nameof(actions));
        }
        if (uiRoot == null)
        {
            throw new ArgumentNullException(nameof(uiRoot));
        }

        viewFactory.Ensure(view, actions, uiRoot);
        viewFactory.ApplyFonts(uiRoot.transform);
    }
}
