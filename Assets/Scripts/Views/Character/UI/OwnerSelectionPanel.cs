using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static OwnerSelectionViewRules;
using VContainer;

public class OwnerSelectionPanel : MonoBehaviour
{
    private enum PreparationTab
    {
        Identity,
        Aptitude,
        Skill
    }

    [SerializeField] private OwnerRunManager ownerRunManager;
    [SerializeField] private Transform optionRoot;
    [SerializeField] private Button optionButtonPrefab;
    [SerializeField] private TMP_Text selectedOwnerText;
    [SerializeField] private bool buildOptionsOnStart = true;

    private readonly Dictionary<int, PreparationTab> selectedTabs = new Dictionary<int, PreparationTab>();
    private readonly Dictionary<int, int> pendingActiveChoices = new Dictionary<int, int>();

    private IOwnerRunManagerProvider ownerRunManagerProvider;
    private ITmpKoreanFontService tmpKoreanFontService;
    private IOwnerSelectionOptionButtonFactory optionButtonFactory;
    private DungeonSceneRuntimeReferences runtimeReferences;
    private IGameSpeedController gameSpeedController;
    private IGameSessionStateProvider gameDataProvider;
    private IStartPartyPreparationService preparationService;
    private IPreparedStartPartyCommitService preparedPartyCommitService;
    private IGameEventBus gameEventBus;
    private IOwnerDoctrineDefinitionCatalog ownerDoctrines;
    private RectTransform surfaceRect;
    private GameObject preparationRoot;
    private GameObject preparationActionsRoot;
    private bool renderingPreparation;

    [Inject]
    public void ConstructOwnerSelectionPanel(
        IOwnerRunManagerProvider ownerRunManagerProvider,
        ITmpKoreanFontService tmpKoreanFontService,
        IOwnerSelectionOptionButtonFactory optionButtonFactory,
        DungeonSceneRuntimeReferences runtimeReferences,
        IGameSpeedController gameSpeedController,
        IGameSessionStateProvider gameDataProvider,
        IStartPartyPreparationService preparationService,
        IPreparedStartPartyCommitService preparedPartyCommitService,
        IGameEventBus gameEventBus,
        IOwnerDoctrineDefinitionCatalog ownerDoctrines)
    {
        this.ownerRunManagerProvider = ownerRunManagerProvider
            ?? throw new ArgumentNullException(nameof(ownerRunManagerProvider));
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
        this.optionButtonFactory = optionButtonFactory
            ?? throw new ArgumentNullException(nameof(optionButtonFactory));
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
        this.gameSpeedController = gameSpeedController
            ?? throw new ArgumentNullException(nameof(gameSpeedController));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.preparationService = preparationService
            ?? throw new ArgumentNullException(nameof(preparationService));
        this.preparedPartyCommitService = preparedPartyCommitService
            ?? throw new ArgumentNullException(nameof(preparedPartyCommitService));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.ownerDoctrines = ownerDoctrines
            ?? throw new ArgumentNullException(nameof(ownerDoctrines));
    }

    private void Start()
    {
        RequireTmpKoreanFontService().ApplyToChildren(transform);
        ConfigureLayout();
        OwnerRunManager manager = ResolveOwnerRunManager();
        preparationService.Changed += HandlePreparationChanged;

        if (buildOptionsOnStart)
        {
            BuildOptions();
        }

        manager.selectedOwnerData ??= new Data<CharacterSO>();
        manager.selectedOwnerData.OnValueChange += HandleSelectedOwnerChanged;
        if (manager.selectedOwnerData.Value != null)
        {
            RefreshSelectedOwner(manager.selectedOwnerData.Value);
            HideIfOwnerSelected(manager.selectedOwnerData.Value);
        }
        else
        {
            PauseForSelection();
        }
    }

    private void OnEnable()
    {
        SetPanelInteraction(true);
    }

    public void BuildOptions()
    {
        OwnerRunManager manager = ResolveOwnerRunManager();
        if (optionRoot == null || optionButtonPrefab == null)
        {
            return;
        }

        DestroyPreparationRoot();
        optionRoot.gameObject.SetActive(true);
        ClearChildren(optionRoot);
        if (selectedOwnerText != null)
        {
            selectedOwnerText.text = "함께 던전을 세울 사장을 선택하세요";
        }

        IReadOnlyList<CharacterSO> candidates = manager.OwnerCandidates;
        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterSO candidate = candidates[i];
            Button button = CreateButton(
                optionRoot,
                $"OwnerOption_{candidate.id}",
                MakeButtonLabel(candidate, ownerDoctrines),
                () => BeginPreparation(candidate),
                240f,
                300f);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Center;
            }
        }
    }

    public void RefreshVisibility()
    {
        OwnerRunManager manager = ResolveOwnerRunManager();
        bool hasOwner = manager.CurrentOwnerActor != null
            || (manager.selectedOwnerData != null && manager.selectedOwnerData.Value != null);
        if (!hasOwner)
        {
            gameObject.SetActive(true);
            SetPanelInteraction(true);
            PauseForSelection();
            if (preparationService != null && preparationService.IsPreparing)
            {
                RenderPreparation();
            }
            else
            {
                BuildOptions();
            }
            return;
        }

        SetPanelInteraction(false);
        gameObject.SetActive(false);
    }

    private void BeginPreparation(CharacterSO ownerData)
    {
        pendingActiveChoices.Clear();
        selectedTabs.Clear();
        if (!preparationService.Begin(ownerData, out string message))
        {
            gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.WARNING);
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            selectedTabs[i] = PreparationTab.Identity;
        }

        RenderPreparation();
    }

    private void HandlePreparationChanged()
    {
        if (preparationService != null && preparationService.IsPreparing)
        {
            RenderPreparation();
        }
    }

    private void RenderPreparation()
    {
        if (renderingPreparation || surfaceRect == null || !preparationService.IsPreparing)
        {
            return;
        }

        renderingPreparation = true;
        try
        {
            optionRoot?.gameObject.SetActive(false);
            DestroyPreparationRoot();
            preparationRoot = CreatePanel("StartPartyPreparation", surfaceRect, Color.clear).gameObject;
            RectTransform rootRect = (RectTransform)preparationRoot.transform;
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.offsetMin = new Vector2(28f, 82f);
            rootRect.offsetMax = new Vector2(-28f, -104f);

            HorizontalLayoutGroup row = preparationRoot.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(0, 0, 0, 0);
            row.spacing = 14f;
            row.childAlignment = TextAnchor.UpperCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;

            foreach (StartPartyMemberPreparation member in preparationService.Members)
            {
                CreateMemberCard(member, preparationRoot.transform);
            }

            if (selectedOwnerText != null)
            {
                selectedOwnerText.text = "시작 파티 준비";
            }

            CreateBottomActions();
            RequireTmpKoreanFontService().ApplyToChildren(preparationRoot.transform);
        }
        finally
        {
            renderingPreparation = false;
        }
    }

    private void CreateMemberCard(StartPartyMemberPreparation member, Transform parent)
    {
        Image card = CreatePanel($"StartPartyMember_{member.Index}", parent, DungeonUiTheme.Surface);
        LayoutElement cardLayout = card.gameObject.AddComponent<LayoutElement>();
        cardLayout.minWidth = 220f;
        cardLayout.flexibleWidth = 1f;

        VerticalLayoutGroup column = card.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(14, 14, 14, 14);
        column.spacing = 9f;
        column.childControlHeight = true;
        column.childControlWidth = true;
        column.childForceExpandHeight = false;
        column.childForceExpandWidth = true;

        string displayName = member.Progression?.GrowthState?.displayName ?? "-";
        TMP_Text header = CreateText(
            card.transform,
            $"{member.RoleLabel}  {displayName}\n{member.CharacterData.SpeciesTag} · 잠재력 {PotentialLabel(member.Progression.PotentialGrade)}",
            22f,
            DungeonUiTheme.TextPrimary,
            TextAlignmentOptions.Left);
        SetLayout(header.gameObject, 62f);

        GameObject tabRow = CreateHorizontalRow(card.transform, "PreparationTabs", 42f, 6f);
        CreateTabButton(member, PreparationTab.Identity, "정체성", tabRow.transform);
        CreateTabButton(member, PreparationTab.Aptitude, "재능", tabRow.transform);
        CreateTabButton(member, PreparationTab.Skill, "스킬", tabRow.transform);

        PreparationTab tab = selectedTabs.TryGetValue(member.Index, out PreparationTab selected)
            ? selected
            : PreparationTab.Identity;
        TMP_Text body = CreateText(
            card.transform,
            BuildTabText(member, tab),
            18f,
            DungeonUiTheme.TextSecondary,
            TextAlignmentOptions.TopLeft);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Truncate;
        SetLayout(body.gameObject, tab == PreparationTab.Skill ? 126f : 250f, 1f);

        if (tab == PreparationTab.Skill)
        {
            CreateSkillChoices(member, card.transform);
        }

        GameObject rerollRow = CreateHorizontalRow(card.transform, "RerollActions", 52f, 6f);
        CreateButton(
            rerollRow.transform,
            $"FullReroll_{member.Index}",
            "전체 리롤",
            () => HandleFullReroll(member.Index),
            96f,
            52f);
        StartPartyRerollGroup group = tab switch
        {
            PreparationTab.Aptitude => StartPartyRerollGroup.Aptitude,
            PreparationTab.Skill => StartPartyRerollGroup.Skill,
            _ => StartPartyRerollGroup.Identity
        };
        int remaining = group switch
        {
            StartPartyRerollGroup.Aptitude => member.AptitudeRerollsRemaining,
            StartPartyRerollGroup.Skill => member.SkillRerollsRemaining,
            _ => member.IdentityRerollsRemaining
        };
        Button partial = CreateButton(
            rerollRow.transform,
            $"PartialReroll_{member.Index}_{group}",
            $"부분 리롤 {remaining}/3",
            () => HandlePartialReroll(member.Index, group),
            120f,
            52f);
        partial.interactable = remaining > 0;
    }

    private void CreateTabButton(
        StartPartyMemberPreparation member,
        PreparationTab tab,
        string label,
        Transform parent)
    {
        Button button = CreateButton(
            parent,
            $"PreparationTab_{member.Index}_{tab}",
            label,
            () =>
            {
                selectedTabs[member.Index] = tab;
                RenderPreparation();
            },
            72f,
            42f);
        PreparationTab selected = selectedTabs.TryGetValue(member.Index, out PreparationTab current)
            ? current
            : PreparationTab.Identity;
        if (selected == tab && button.targetGraphic is Image image)
        {
            image.color = DungeonUiTheme.Accent;
        }
    }

    private void CreateSkillChoices(StartPartyMemberPreparation member, Transform parent)
    {
        CharacterSkillDraft draft = member.Progression.Drafts.FirstOrDefault(candidate => candidate != null
            && candidate.kind == CharacterSkillKind.Active
            && candidate.unlockLevel == 1
            && candidate.isReady);
        if (draft == null)
        {
            GameObject spacer = new GameObject("SkillChoiceSpace", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = 190f;
            return;
        }

        for (int i = 0; i < draft.candidates.Count; i++)
        {
            CharacterSkillInstance skill = draft.candidates[i];
            int candidateIndex = i;
            bool selected = member.Progression.ActiveSkills.Any(active => active != null && active.id == skill.id);
            bool awaitingConfirmation = pendingActiveChoices.TryGetValue(member.Index, out int pending)
                && pending == candidateIndex;
            string suffix = selected
                ? "  [확정]"
                : awaitingConfirmation ? "  [다시 눌러 확정]" : string.Empty;
            Button button = CreateButton(
                parent,
                $"StartSkillCandidate_{member.Index}_{candidateIndex}",
                $"{RarityLabel(skill.rarity)} · {skill.displayName}{suffix}\n{skill.description}",
                () => HandleSkillCandidate(member.Index, candidateIndex),
                160f,
                58f);
            button.interactable = !member.HasSelectedFirstActive;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Left;
            }
        }
    }

    private void CreateBottomActions()
    {
        preparationActionsRoot = CreatePanel(
            "StartPartyActions",
            surfaceRect,
            Color.clear).gameObject;
        RectTransform actionsRect = (RectTransform)preparationActionsRoot.transform;
        actionsRect.anchorMin = Vector2.zero;
        actionsRect.anchorMax = Vector2.one;
        actionsRect.offsetMin = Vector2.zero;
        actionsRect.offsetMax = Vector2.zero;
        preparationActionsRoot.GetComponent<Image>().raycastTarget = false;

        Button back = CreateButton(
            actionsRect,
            "StartPartyBack",
            "사장 다시 선택",
            () =>
            {
                preparationService.Cancel();
                pendingActiveChoices.Clear();
                BuildOptions();
            },
            190f,
            54f);
        RectTransform backRect = (RectTransform)back.transform;
        backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 0f);
        backRect.pivot = new Vector2(0f, 0f);
        backRect.anchoredPosition = new Vector2(28f, 18f);

        bool canStart = preparationService.Members.Count == 3
            && preparationService.Members.All(member => member.IsReadyToStart);
        Button start = CreateButton(
            actionsRect,
            "StartPartyConfirm",
            canStart ? "이 파티로 시작" : "세 캐릭터의 기술 선택 필요",
            CommitParty,
            280f,
            54f);
        RectTransform startRect = (RectTransform)start.transform;
        startRect.anchorMin = startRect.anchorMax = new Vector2(1f, 0f);
        startRect.pivot = new Vector2(1f, 0f);
        startRect.anchoredPosition = new Vector2(-28f, 18f);
        start.interactable = canStart;
        if (canStart && start.targetGraphic is Image image)
        {
            image.color = DungeonUiTheme.Accent;
        }
    }

    private string BuildTabText(StartPartyMemberPreparation member, PreparationTab tab)
    {
        CharacterProgression progression = member.Progression;
        if (tab == PreparationTab.Identity)
        {
            string traits = string.Join("\n", progression.ResolveSelectedTraits()
                .Where(trait => trait != null)
                .Select(trait => $"· {trait.traitName}: {trait.description}"));
            return $"출신\n{progression.GrowthState.origin}\n\n특성\n{traits}";
        }

        if (tab == PreparationTab.Aptitude)
        {
            IReadOnlyList<CharacterStartingProficiencyExperience> starts =
                progression.GrowthState.startingProficiencies;
            starts ??= Array.Empty<CharacterStartingProficiencyExperience>();
            string[] values = BuiltInCharacterProficiencyIds.All
                .Select(id =>
                {
                    int experience = starts.FirstOrDefault(value =>
                        value != null
                        && string.Equals(
                            value.proficiencyId,
                            id.Value,
                            StringComparison.Ordinal))?.experience ?? 0;
                    return $"{StartPartyPreparationPresentation.ProficiencyLabel(id)} {experience} XP";
                })
                .ToArray();
            return $"잠재력  {PotentialLabel(progression.PotentialGrade)}\n\n"
                + string.Join("\n", values)
                + "\n\n숙련이 성장하면 관련 작업 속도·완성 품질·사고 위험과 전투 성능이 변합니다.";
        }

        string passive = progression.PassiveSkills.FirstOrDefault()?.displayName ?? "-";
        string active = progression.ActiveSkills.FirstOrDefault()?.displayName ?? "선택 전";
        return $"종족 액티브  {member.CharacterData.SpeciesTag} 고유기\n첫 패시브  {passive}\n첫 액티브  {active}";
    }

    private void HandleFullReroll(int memberIndex)
    {
        pendingActiveChoices.Remove(memberIndex);
        if (!preparationService.TryFullReroll(memberIndex, out string message))
        {
            gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.WARNING);
        }
    }

    private void HandlePartialReroll(int memberIndex, StartPartyRerollGroup group)
    {
        pendingActiveChoices.Remove(memberIndex);
        if (!preparationService.TryPartialReroll(memberIndex, group, out string message))
        {
            gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.WARNING);
        }
    }

    private void HandleSkillCandidate(int memberIndex, int candidateIndex)
    {
        if (!pendingActiveChoices.TryGetValue(memberIndex, out int pending) || pending != candidateIndex)
        {
            pendingActiveChoices[memberIndex] = candidateIndex;
            RenderPreparation();
            return;
        }

        if (!preparationService.TryChooseFirstActive(memberIndex, candidateIndex, out string message))
        {
            gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.WARNING);
            return;
        }

        pendingActiveChoices.Remove(memberIndex);
    }

    private void CommitParty()
    {
        if (!preparedPartyCommitService.TryCommit(out string message))
        {
            gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.WARNING);
            return;
        }

        gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.NONE);
    }

    private OwnerRunManager ResolveOwnerRunManager()
    {
        if (ownerRunManager != null)
        {
            return ownerRunManager;
        }

        if (ownerRunManagerProvider == null
            || !ownerRunManagerProvider.TryGetManager(out OwnerRunManager resolvedManager))
        {
            throw new InvalidOperationException($"{nameof(OwnerSelectionPanel)} could not resolve {nameof(OwnerRunManager)}.");
        }

        ownerRunManager = resolvedManager;
        return ownerRunManager;
    }

    private void RefreshSelectedOwner(CharacterSO ownerData)
    {
        if (selectedOwnerText == null)
        {
            return;
        }

        RequireTmpKoreanFontService().Apply(selectedOwnerText);
        selectedOwnerText.text = ownerData != null
            ? $"{ownerData.characterName}\n{ownerData.ownerSummary}"
            : "함께 던전을 세울 사장을 선택하세요";
    }

    private void HandleSelectedOwnerChanged(CharacterSO ownerData)
    {
        RefreshSelectedOwner(ownerData);
        HideIfOwnerSelected(ownerData);
        if (ownerData != null && !IsSaveModalOpen())
        {
            ResumeAfterSelection();
        }
    }

    private void HideIfOwnerSelected(CharacterSO ownerData)
    {
        if (ownerData == null)
        {
            return;
        }

        SetPanelInteraction(false);
        gameObject.SetActive(false);
    }

    private void SetPanelInteraction(bool value)
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }

        group.interactable = value;
        group.blocksRaycasts = value;
        if (TryGetComponent(out Image scrim))
        {
            scrim.raycastTarget = value;
        }
    }

    private ITmpKoreanFontService RequireTmpKoreanFontService()
    {
        return tmpKoreanFontService
            ?? throw new InvalidOperationException($"{nameof(OwnerSelectionPanel)} requires {nameof(ITmpKoreanFontService)} injection.");
    }

    private IOwnerSelectionOptionButtonFactory RequireOptionButtonFactory()
    {
        return optionButtonFactory
            ?? throw new InvalidOperationException($"{nameof(OwnerSelectionPanel)} requires {nameof(IOwnerSelectionOptionButtonFactory)} injection.");
    }

    private void ConfigureLayout()
    {
        if (!(transform is RectTransform rootRect))
        {
            return;
        }

        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        if (TryGetComponent(out Image scrim))
        {
            scrim.color = DungeonUiTheme.OwnerSelectionScrim;
            scrim.raycastTarget = true;
        }

        Transform surface = transform.Find("OwnerSelectionSurface");
        if (surface == null)
        {
            surface = CreatePanel("OwnerSelectionSurface", transform, DungeonUiTheme.Panel).transform;
        }

        surfaceRect = (RectTransform)surface;
        surfaceRect.anchorMin = new Vector2(0.035f, 0.055f);
        surfaceRect.anchorMax = new Vector2(0.965f, 0.945f);
        surfaceRect.pivot = new Vector2(0.5f, 0.5f);
        surfaceRect.offsetMin = Vector2.zero;
        surfaceRect.offsetMax = Vector2.zero;

        if (selectedOwnerText != null)
        {
            selectedOwnerText.transform.SetParent(surface, false);
            RectTransform titleRect = selectedOwnerText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -22f);
            titleRect.sizeDelta = new Vector2(-56f, 68f);
            selectedOwnerText.text = "함께 던전을 세울 사장을 선택하세요";
            selectedOwnerText.fontSize = 30f;
            selectedOwnerText.fontStyle = FontStyles.Bold;
            selectedOwnerText.alignment = TextAlignmentOptions.Center;
            selectedOwnerText.color = DungeonUiTheme.TextPrimary;
        }

        if (optionRoot is RectTransform optionsRect)
        {
            optionRoot.SetParent(surface, false);
            optionsRect.anchorMin = new Vector2(0f, 0f);
            optionsRect.anchorMax = new Vector2(1f, 1f);
            optionsRect.offsetMin = new Vector2(38f, 38f);
            optionsRect.offsetMax = new Vector2(-38f, -112f);
            HorizontalLayoutGroup layout = optionRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 14f;
                layout.childAlignment = TextAnchor.MiddleCenter;
            }
        }

        if (optionButtonPrefab != null)
        {
            optionButtonPrefab.transform.SetParent(surface, false);
            optionButtonPrefab.gameObject.SetActive(false);
        }

        transform.SetAsLastSibling();
    }

    private Button CreateButton(
        Transform parent,
        string objectName,
        string label,
        Action onClick,
        float preferredWidth,
        float preferredHeight)
    {
        Button button = RequireOptionButtonFactory().Create(
            optionButtonPrefab,
            parent,
            objectName,
            label,
            () => onClick?.Invoke());
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(preferredWidth, preferredHeight);
        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = button.gameObject.AddComponent<LayoutElement>();
        }

        layout.preferredWidth = preferredWidth;
        layout.preferredHeight = preferredHeight;
        layout.flexibleWidth = 1f;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = 19f;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
        }

        return button;
    }

    private TMP_Text CreateText(
        Transform parent,
        string value,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        RequireTmpKoreanFontService().Apply(text);
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            RequireOptionButtonFactory().Release(root.GetChild(i).gameObject);
        }
    }

    private void DestroyPreparationRoot()
    {
        if (preparationRoot != null)
        {
            preparationRoot.SetActive(false);
            Destroy(preparationRoot);
            preparationRoot = null;
        }

        if (preparationActionsRoot != null)
        {
            preparationActionsRoot.SetActive(false);
            Destroy(preparationActionsRoot);
            preparationActionsRoot = null;
        }
    }

    private void PauseForSelection()
    {
        gameSpeedController.SetPaused(true);
    }

    private void ResumeAfterSelection()
    {
        gameSpeedController.SetPaused(false);
    }

    private void OnDestroy()
    {
        if (preparationService != null)
        {
            preparationService.Changed -= HandlePreparationChanged;
        }

        if (ownerRunManager != null && ownerRunManager.selectedOwnerData != null)
        {
            ownerRunManager.selectedOwnerData.OnValueChange -= HandleSelectedOwnerChanged;
        }
    }
}
