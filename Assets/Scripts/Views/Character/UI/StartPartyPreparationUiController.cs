using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer.Unity;
using static StartPartyPreparationViewFactory;
using static StartPartyPreparationPresentation;

public sealed class StartPartyPreparationUiController : IStartable, IDisposable
{
    private enum PreparationScreen
    {
        OwnerSelect,
        PartyPrepare
    }

    private readonly IOwnerCandidateCatalog ownerCandidateCatalog;
    private readonly IStartPartyPreparationService preparationService;
    private readonly IDungeonSceneNavigator sceneNavigator;
    private readonly IDungeonUiCanvasProvider canvasProvider;
    private readonly ITmpKoreanFontService fontService;
    private readonly IUiClock uiClock;
    private readonly IDungeonUserSettingsService userSettings;
    private readonly StartPartyPreparationViewFactory viewFactory;
    private readonly StartPartyMemberDetailRenderer memberDetailRenderer;

    private CharacterSO[] ownerCandidates = Array.Empty<CharacterSO>();
    private DungeonPreparationLaunchRequest launchRequest;
    private PreparationScreen screen = PreparationScreen.OwnerSelect;
    private CharacterSO selectedOwner;
    private int selectedOwnerSkillIndex;
    private int selectedMemberIndex = 1;
    private GameObject root;
    private GameObject contentRoot;
    private TMP_Text statusText;
    private string lastStatusMessage = string.Empty;
    private bool lastStatusIsError;
    private int draggingMemberIndex = -1;
    private bool rendering;

    public StartPartyPreparationUiController(
        IOwnerCandidateCatalog ownerCandidateCatalog,
        IStartPartyPreparationService preparationService,
        IDungeonSceneNavigator sceneNavigator,
        IDungeonUiCanvasProvider canvasProvider,
        ITmpKoreanFontService fontService,
        IUiClock uiClock,
        IDungeonUserSettingsService userSettings)
    {
        this.ownerCandidateCatalog = ownerCandidateCatalog
            ?? throw new ArgumentNullException(nameof(ownerCandidateCatalog));
        this.preparationService = preparationService
            ?? throw new ArgumentNullException(nameof(preparationService));
        this.sceneNavigator = sceneNavigator
            ?? throw new ArgumentNullException(nameof(sceneNavigator));
        this.canvasProvider = canvasProvider
            ?? throw new ArgumentNullException(nameof(canvasProvider));
        this.fontService = fontService
            ?? throw new ArgumentNullException(nameof(fontService));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.userSettings = userSettings
            ?? throw new ArgumentNullException(nameof(userSettings));
        viewFactory = new StartPartyPreparationViewFactory(fontService);
        memberDetailRenderer = new StartPartyMemberDetailRenderer(
            viewFactory,
            Render,
            Reroll,
            Swap);
    }

    public void Start()
    {
        sceneNavigator.TryConsumePreparationLaunch(out launchRequest);
        ownerCandidates = ownerCandidateCatalog.OwnerCandidates
            .Where(candidate => candidate != null)
            .OrderBy(candidate => candidate.id)
            .ToArray();
        selectedOwner = ownerCandidates.FirstOrDefault();
        preparationService.Changed += HandlePreparationChanged;

        Canvas canvas = canvasProvider.GetOrCreateCanvas();
        root = new GameObject("StartPreparationRuntimeUI", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        Stretch(root.GetComponent<RectTransform>());
        DungeonUiThemeRuntime.Ensure(
            canvas,
            fontService,
            uiClock,
            userSettings).ApplyNow();
        Render();
    }

    public void Dispose()
    {
        preparationService.Changed -= HandlePreparationChanged;
        preparationService.Cancel();
        memberDetailRenderer.HideTooltip();
        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
        }
    }

    private void HandlePreparationChanged()
    {
        if (!rendering)
        {
            Render();
        }
    }

    private void Render()
    {
        if (root == null)
        {
            return;
        }

        rendering = true;
        memberDetailRenderer.HideTooltip();
        if (contentRoot != null)
        {
            UnityEngine.Object.Destroy(contentRoot);
        }

        Image background = root.GetComponent<Image>();
        if (background == null)
        {
            background = root.AddComponent<Image>();
        }

        background.color = DungeonUiTheme.SurfaceMuted;
        background.raycastTarget = true;

        contentRoot = new GameObject("PreparationContent", typeof(RectTransform));
        contentRoot.transform.SetParent(root.transform, false);
        Stretch(contentRoot.GetComponent<RectTransform>());

        if (screen == PreparationScreen.OwnerSelect)
        {
            RenderOwnerSelect(contentRoot.transform);
        }
        else
        {
            RenderPartyPrepare(contentRoot.transform);
        }

        statusText = CreateText(contentRoot.transform, "PreparationStatus", string.Empty, 18f, TextAlignmentOptions.BottomLeft);
        SetRect(statusText.rectTransform, new Vector2(0.055f, 0.025f), new Vector2(0.63f, 0.075f));
        statusText.color = DungeonUiTheme.TextSecondary;
        ApplyStatusText();
        rendering = false;
    }

    private void RenderOwnerSelect(Transform parent)
    {
        TMP_Text title = CreateText(parent, "PreparationOwnerTitle", "\uC0AC\uC7A5 \uC120\uD0DD", 38f, TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(0.055f, 0.9f), new Vector2(0.4f, 0.97f));
        title.fontStyle = FontStyles.Bold;

        Transform list = CreatePanel(parent, "OwnerCandidateList", new Vector2(0.035f, 0.13f), new Vector2(0.18f, 0.87f), true);
        for (int i = 0; i < ownerCandidates.Length; i++)
        {
            CharacterSO candidate = ownerCandidates[i];
            float top = 0.96f - i * 0.135f;
            float bottom = top - 0.115f;
            Button button = CreateButton(
                list,
                "OwnerCandidate_" + candidate.id,
                candidate.characterName,
                () =>
                {
                    selectedOwner = candidate;
                    selectedOwnerSkillIndex = 0;
                    Render();
                },
                new Vector2(0.08f, Mathf.Max(0.02f, bottom)),
                new Vector2(0.92f, Mathf.Max(0.12f, top)),
                selectedOwner == candidate);
            button.image.color = selectedOwner == candidate
                ? DungeonUiTheme.Accent
                : DungeonUiTheme.SurfaceRaised;
        }

        Transform center = CreatePanel(parent, "OwnerStage", new Vector2(0.205f, 0.13f), new Vector2(0.66f, 0.87f), false);
        center.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.03f, 0.92f);
        RenderOwnerStage(center);

        Transform detail = CreatePanel(parent, "OwnerFixedSkillDetail", new Vector2(0.69f, 0.13f), new Vector2(0.965f, 0.87f), true);
        RenderOwnerDetail(detail);

        CreateButton(parent, "PreparationBackToTitleButton", "\uB3CC\uC544\uAC00\uAE30", () => sceneNavigator.LoadTitle(),
            new Vector2(0.035f, 0.035f), new Vector2(0.16f, 0.095f));
        Button next = CreateButton(parent, "PreparationOwnerNextButton", "\uB2E4\uC74C", BeginPartyPrepare,
            new Vector2(0.835f, 0.035f), new Vector2(0.965f, 0.095f), selected: true);
        next.interactable = selectedOwner != null;
    }

    private void RenderOwnerStage(Transform parent)
    {
        CharacterSO owner = selectedOwner;
        TMP_Text name = CreateText(parent, "OwnerName", owner != null ? owner.characterName : "-", 42f, TextAlignmentOptions.Center);
        SetRect(name.rectTransform, new Vector2(0.07f, 0.82f), new Vector2(0.93f, 0.94f));
        name.fontStyle = FontStyles.Bold;

        Image portrait = CreateImage(parent, "OwnerPortrait", DungeonUiTheme.SurfaceRaised);
        SetRect(portrait.rectTransform, new Vector2(0.18f, 0.25f), new Vector2(0.82f, 0.78f));
        portrait.sprite = owner != null ? owner.characterSprite : null;
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        IReadOnlyList<CharacterSkillInstance> skills = CharacterOwnerFixedSkillUtility.GetSkills(owner);
        for (int i = 0; i < CharacterOwnerFixedSkillUtility.FixedSlotCount; i++)
        {
            int skillIndex = i;
            float left = 0.19f + i * 0.16f;
            Button skillButton = CreateButton(
                parent,
                "OwnerFixedSkill_" + i,
                skills[i].displayName,
                () =>
                {
                    selectedOwnerSkillIndex = skillIndex;
                    Render();
                },
                new Vector2(left, 0.08f),
                new Vector2(left + 0.13f, 0.19f),
                selectedOwnerSkillIndex == i);
            TMP_Text label = skillButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.fontSize = 15f;
                label.enableAutoSizing = true;
                label.fontSizeMin = 11f;
                label.fontSizeMax = 15f;
            }
        }
    }

    private void RenderOwnerDetail(Transform parent)
    {
        CharacterSO owner = selectedOwner;
        IReadOnlyList<CharacterSkillInstance> skills = CharacterOwnerFixedSkillUtility.GetSkills(owner);
        CharacterSkillInstance skill = skills[Mathf.Clamp(selectedOwnerSkillIndex, 0, skills.Count - 1)];

        TMP_Text heading = CreateText(parent, "OwnerDetailHeading", "\uC0AC\uC7A5 \uACE0\uC815 \uC2A4\uD0AC", 24f, TextAlignmentOptions.MidlineLeft);
        SetRect(heading.rectTransform, new Vector2(0.07f, 0.88f), new Vector2(0.92f, 0.96f));
        heading.fontStyle = FontStyles.Bold;

        TMP_Text skillName = CreateText(parent, "OwnerSkillName", skill.displayName, 27f, TextAlignmentOptions.MidlineLeft);
        SetRect(skillName.rectTransform, new Vector2(0.07f, 0.76f), new Vector2(0.92f, 0.86f));
        skillName.color = DungeonUiTheme.Accent;
        skillName.fontStyle = FontStyles.Bold;

        TMP_Text description = CreateText(parent, "OwnerSkillDescription", skill.description, 18f, TextAlignmentOptions.TopLeft);
        SetRect(description.rectTransform, new Vector2(0.07f, 0.54f), new Vector2(0.92f, 0.75f));
        description.textWrappingMode = TextWrappingModes.Normal;

        string ownerInfo = owner == null
            ? string.Empty
            : $"{owner.SpeciesTag}\n{owner.ownerSummary}\n\uC120\uD638 \uC5C5\uBB34: {FormatWorkTypes(owner.OwnerPreferredWorkTypeIds)}";
        TMP_Text info = CreateText(parent, "OwnerInfo", ownerInfo, 17f, TextAlignmentOptions.TopLeft);
        SetRect(info.rectTransform, new Vector2(0.07f, 0.12f), new Vector2(0.92f, 0.48f));
        info.color = DungeonUiTheme.TextSecondary;
        info.textWrappingMode = TextWrappingModes.Normal;
    }

    private void RenderPartyPrepare(Transform parent)
    {
        TMP_Text title = CreateText(parent, "PartyPrepareTitle", "\uC2DC\uC791 \uD30C\uD2F0 \uC900\uBE44", 34f, TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(0.035f, 0.91f), new Vector2(0.4f, 0.975f));
        title.fontStyle = FontStyles.Bold;

        Transform roster = CreatePanel(parent, "PartyRosterPanel", new Vector2(0.025f, 0.15f), new Vector2(0.28f, 0.89f), true);
        RenderRoster(roster);

        Transform detail = CreatePanel(parent, "PartyDetailPanel", new Vector2(0.305f, 0.15f), new Vector2(0.965f, 0.89f), true);
        StartPartyMemberPreparation selectedMember = ResolveSelectedMember();
        memberDetailRenderer.Render(
            detail,
            root.transform,
            selectedMember,
            selectedMember != null
                ? GetMemberReadyLabel(selectedMember)
                : string.Empty);

        Transform team = CreatePanel(parent, "TeamSummaryPanel", new Vector2(0.305f, 0.035f), new Vector2(0.74f, 0.13f), false);
        team.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;
        TMP_Text summary = CreateText(team, "TeamSummaryText", BuildTeamSummary(), 15f, TextAlignmentOptions.TopLeft);
        SetRect(summary.rectTransform, new Vector2(0.035f, 0.12f), new Vector2(0.965f, 0.88f));
        summary.color = DungeonUiTheme.TextSecondary;
        summary.textWrappingMode = TextWrappingModes.Normal;

        CreateButton(parent, "PartyBackToOwnerButton", "\uC0AC\uC7A5 \uB2E4\uC2DC \uC120\uD0DD", BackToOwnerSelect,
            new Vector2(0.025f, 0.035f), new Vector2(0.18f, 0.11f));
        bool canStart = CanStartPreparedRun();
        string startLabel = canStart
            ? "\uC2DC\uC791"
            : "\uC900\uBE44 \uD544\uC694";
        Button start = CreateButton(parent, "PreparationStartRunButton", startLabel, StartPreparedRun,
            new Vector2(0.825f, 0.035f), new Vector2(0.965f, 0.11f), selected: canStart);
        start.interactable = canStart;
        start.image.color = canStart
            ? DungeonUiTheme.Accent
            : DungeonUiTheme.SurfaceRaised;
    }

    private void RenderRoster(Transform parent)
    {
        TMP_Text selectedHeading = CreateText(parent, "SelectedHeading", "\uC120\uBC1C", 20f, TextAlignmentOptions.MidlineLeft);
        SetRect(selectedHeading.rectTransform, new Vector2(0.07f, 0.91f), new Vector2(0.92f, 0.97f));
        selectedHeading.fontStyle = FontStyles.Bold;

        IReadOnlyList<StartPartyMemberPreparation> selectedMembers = preparationService.Members;
        for (int i = 0; i < selectedMembers.Count; i++)
        {
            StartPartyMemberPreparation member = selectedMembers[i];
            CreateRosterButton(parent, member, 0.78f - i * 0.12f, 0.11f);
        }

        TMP_Text reserveHeading = CreateText(parent, "ReserveHeading", "\uC608\uBE44", 20f, TextAlignmentOptions.MidlineLeft);
        SetRect(reserveHeading.rectTransform, new Vector2(0.07f, 0.49f), new Vector2(0.92f, 0.55f));
        reserveHeading.fontStyle = FontStyles.Bold;

        IReadOnlyList<StartPartyMemberPreparation> reserves = preparationService.Reserves;
        for (int i = 0; i < reserves.Count; i++)
        {
            CreateRosterButton(parent, reserves[i], 0.37f - i * 0.095f, 0.085f);
        }
    }

    private void CreateRosterButton(
        Transform parent,
        StartPartyMemberPreparation member,
        float bottom,
        float height)
    {
        if (member == null)
        {
            return;
        }

        Button button = CreateButton(
            parent,
            "PreparationRosterCard_" + member.Index,
            BuildRosterLabel(member),
            () =>
            {
                selectedMemberIndex = member.Index;
                Render();
            },
            new Vector2(0.07f, bottom),
            new Vector2(0.93f, bottom + height),
            selectedMemberIndex == member.Index);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.alignment = TextAlignmentOptions.Left;
            label.fontSize = 16f;
            label.textWrappingMode = TextWrappingModes.Normal;
        }

        StartPartyRosterDragHandler dragHandler = button.gameObject.AddComponent<StartPartyRosterDragHandler>();
        dragHandler.Bind(this, member.Index);
        CanvasGroup canvasGroup = button.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = draggingMemberIndex == member.Index ? 0.68f : 1f;
    }

    private void BeginPartyPrepare()
    {
        if (selectedOwner == null)
        {
            SetStatus("\uC0AC\uC7A5\uC744 \uBA3C\uC800 \uC120\uD0DD\uD558\uC138\uC694.", true);
            return;
        }

        if (!preparationService.Begin(selectedOwner, out string message))
        {
            SetStatus(message, true);
            return;
        }

        screen = PreparationScreen.PartyPrepare;
        selectedMemberIndex = preparationService.Members.Skip(1).FirstOrDefault()?.Index
            ?? preparationService.Members.FirstOrDefault()?.Index
            ?? 0;
        memberDetailRenderer.ResetTab();
        SetStatus(message, false);
        Render();
    }

    private void BackToOwnerSelect()
    {
        preparationService.Cancel();
        screen = PreparationScreen.OwnerSelect;
        memberDetailRenderer.ResetTab();
        selectedMemberIndex = 1;
        Render();
    }

    private void Reroll(StartPartyMemberPreparation member, StartPartyRerollGroup? group)
    {
        string partialMessage;
        bool ok = group.HasValue
            ? preparationService.TryPartialReroll(member.Index, group.Value, out partialMessage)
            : preparationService.TryFullReroll(member.Index, out partialMessage);
        SetStatus(partialMessage, !ok);
    }

    private void Swap(StartPartyMemberPreparation reserve, int partySlot)
    {
        StartPartyMemberPreparation selected = preparationService.Members
            .FirstOrDefault(member => member != null && !member.IsOwner && member.PartySlot == partySlot);
        if (selected == null)
        {
            SetStatus("\uAD50\uCCB4\uD560 \uC120\uBC1C \uC9C1\uC6D0\uC744 \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.", true);
            return;
        }

        bool ok = preparationService.TrySwapWithReserve(selected.Index, reserve.Index, out string message);
        if (ok)
        {
            selectedMemberIndex = reserve.Index;
        }

        SetStatus(message, !ok);
    }

    private void StartPreparedRun()
    {
        if (!preparationService.TryCreatePreparedSnapshot(
                launchRequest.Difficulty,
                launchRequest.SurvivalPressure,
                launchRequest.RunSeed,
                out PreparedStartPartySnapshot snapshot,
                out string message))
        {
            SetStatus(message, true);
            return;
        }

        if (!sceneNavigator.StartPreparedNewGame(snapshot))
        {
            SetStatus("\uAC8C\uC784 \uC2E0\uC73C\uB85C \uC9C4\uC785\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.", true);
            return;
        }

        SetStatus(message, false);
    }

    private StartPartyMemberPreparation ResolveSelectedMember()
    {
        StartPartyMemberPreparation selected = preparationService.Roster
            .FirstOrDefault(member => member != null && member.Index == selectedMemberIndex);
        if (selected != null)
        {
            return selected;
        }

        selected = preparationService.Members.Skip(1).FirstOrDefault()
            ?? preparationService.Members.FirstOrDefault();
        if (selected != null)
        {
            selectedMemberIndex = selected.Index;
        }

        return selected;
    }

    private bool CanStartPreparedRun()
    {
        return preparationService.Members.Count == 3
            && preparationService.Members.All(member => member.IsReadyToStart);
    }

    private string GetStartBlockReason()
    {
        IReadOnlyList<StartPartyMemberPreparation> members = preparationService.Members;
        if (!preparationService.IsPreparing || members.Count != 3)
        {
            return "\uD30C\uD2F0 \uAD6C\uC131\uC774 \uC644\uC131\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.";
        }

        StartPartyMemberPreparation incomplete = members
            .FirstOrDefault(member => member != null && !member.IsReadyToStart);
        if (incomplete == null)
        {
            return "\uC2DC\uC791 \uC900\uBE44 \uC644\uB8CC";
        }

        if (incomplete.IsOwner)
        {
            return "\uC0AC\uC7A5 \uACE0\uC815 \uC2A4\uD0AC\uC744 \uD655\uC778\uD558\uC138\uC694.";
        }

        if (!incomplete.HasReadyFirstActive)
        {
            return $"{incomplete.RosterLabel}: \uCD08\uAE30 \uC561\uD2F0\uBE0C \uC0DD\uC131 \uD544\uC694";
        }

        if (!incomplete.HasSelectedFirstActive)
        {
            return $"{incomplete.RosterLabel}: \uCD08\uAE30 \uC561\uD2F0\uBE0C \uD655\uC815 \uD544\uC694";
        }

        return $"{incomplete.RosterLabel}: \uCCAB \uD328\uC2DC\uBE0C \uC0DD\uC131 \uD544\uC694";
    }

    private string GetMemberReadyLabel(StartPartyMemberPreparation member)
    {
        if (member == null)
        {
            return "-";
        }

        if (member.IsReadyToStart)
        {
            return member.IsOwner
                ? "\uC0AC\uC7A5 \uAD8C\uB2A5 \uC900\uBE44"
                : "\uC120\uBC1C \uC900\uBE44 \uC644\uB8CC";
        }

        if (member.IsOwner)
        {
            return "\uC0AC\uC7A5 \uAD8C\uB2A5 \uD655\uC778 \uD544\uC694";
        }

        if (!member.HasSelectedFirstActive)
        {
            return "\uCD08\uAE30 \uC561\uD2F0\uBE0C \uD655\uC815 \uD544\uC694";
        }

        return "\uD328\uC2DC\uBE0C \uC0DD\uC131 \uD544\uC694";
    }

    private void BeginRosterDrag(int memberIndex)
    {
        draggingMemberIndex = memberIndex;
        StartPartyMemberPreparation member = preparationService.Roster
            .FirstOrDefault(item => item != null && item.Index == memberIndex);
        if (member == null)
        {
            return;
        }

        SetStatus($"{member.RosterLabel}\uC744 \uB4DC\uB798\uADF8\uD574 \uC120\uBC1C/\uC608\uBE44\uB97C \uAD50\uCCB4\uD558\uC138\uC694.", false);
    }

    private void EndRosterDrag()
    {
        draggingMemberIndex = -1;
    }

    private void DropRosterDrag(int targetMemberIndex)
    {
        if (draggingMemberIndex < 0 || draggingMemberIndex == targetMemberIndex)
        {
            return;
        }

        StartPartyMemberPreparation source = preparationService.Roster
            .FirstOrDefault(item => item != null && item.Index == draggingMemberIndex);
        StartPartyMemberPreparation target = preparationService.Roster
            .FirstOrDefault(item => item != null && item.Index == targetMemberIndex);
        if (source == null || target == null)
        {
            SetStatus("\uAD50\uCCB4\uD560 \uCE90\uB9AD\uD130\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.", true);
            return;
        }

        StartPartyMemberPreparation selected = source.IsReserve ? target : source;
        StartPartyMemberPreparation reserve = source.IsReserve ? source : target;
        if (selected.IsOwner || selected.IsReserve || !reserve.IsReserve)
        {
            SetStatus("\uC120\uBC1C \uC9C1\uC6D0\uACFC \uC608\uBE44 \uC9C1\uC6D0\uB9CC \uAD50\uCCB4\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.", true);
            return;
        }

        bool ok = preparationService.TrySwapWithReserve(selected.Index, reserve.Index, out string message);
        if (ok)
        {
            selectedMemberIndex = reserve.Index;
        }

        SetStatus(message, !ok);
    }

    private string BuildTeamSummary()
    {
        IReadOnlyList<StartPartyMemberPreparation> members = preparationService.Members;
        int ready = members.Count(member => member.IsReadyToStart);
        string names = string.Join(", ", members.Select(ResolveMemberName));
        string reason = CanStartPreparedRun()
            ? "\uC2DC\uC791 \uAC00\uB2A5"
            : GetStartBlockReason();
        return $"\uD300 \uC900\uBE44 {ready}/{members.Count} - {names}\n{reason}";
    }

    private void SetStatus(string message, bool error)
    {
        lastStatusMessage = message ?? string.Empty;
        lastStatusIsError = error;
        ApplyStatusText();
    }

    private void ApplyStatusText()
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = lastStatusMessage;
        statusText.color = lastStatusIsError ? DungeonUiTheme.Danger : DungeonUiTheme.TextSecondary;
    }

    private Button CreateDiceButton(
        Transform parent,
        string name,
        Action clicked,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool interactable,
        string accessibleLabel) =>
        viewFactory.CreateDiceButton(
            parent,
            name,
            clicked,
            anchorMin,
            anchorMax,
            interactable,
            accessibleLabel);

    private TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        float size,
        TextAlignmentOptions alignment) =>
        viewFactory.CreateText(parent, name, text, size, alignment);

    private Image CreateImage(Transform parent, string name, Color color) =>
        viewFactory.CreateImage(parent, name, color);

    private Transform CreatePanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool raised) =>
        viewFactory.CreatePanel(parent, name, anchorMin, anchorMax, raised);

    private Button CreateButton(
        Transform parent,
        string name,
        string label,
        Action clicked,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool selected = false) =>
        viewFactory.CreateButton(
            parent,
            name,
            label,
            clicked,
            anchorMin,
            anchorMax,
            selected);

    private sealed class StartPartyRosterDragHandler :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler
    {
        private StartPartyPreparationUiController controller;
        private CanvasGroup canvasGroup;
        private int memberIndex;

        public void Bind(StartPartyPreparationUiController owner, int index)
        {
            controller = owner;
            memberIndex = index;
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            controller?.BeginRosterDrag(memberIndex);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0.68f;
                canvasGroup.blocksRaycasts = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
            }

            controller?.EndRosterDrag();
        }

        public void OnDrop(PointerEventData eventData)
        {
            controller?.DropRosterDrag(memberIndex);
        }
    }
}
