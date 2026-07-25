using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer.Unity;

public sealed class StartPartyPreparationUiController : IStartable, IDisposable
{
    private enum PreparationScreen
    {
        OwnerSelect,
        PartyPrepare
    }

    private enum DetailTab
    {
        Identity,
        Aptitude,
        Skill
    }

    private readonly IOwnerCandidateCatalog ownerCandidateCatalog;
    private readonly IStartPartyPreparationService preparationService;
    private readonly IDungeonSceneNavigator sceneNavigator;
    private readonly IDungeonUiCanvasProvider canvasProvider;
    private readonly ITmpKoreanFontService fontService;
    private readonly IUiClock uiClock;

    private CharacterSO[] ownerCandidates = Array.Empty<CharacterSO>();
    private DungeonPreparationLaunchRequest launchRequest;
    private PreparationScreen screen = PreparationScreen.OwnerSelect;
    private DetailTab selectedTab = DetailTab.Identity;
    private CharacterSO selectedOwner;
    private int selectedOwnerSkillIndex;
    private int selectedMemberIndex = 1;
    private GameObject root;
    private GameObject contentRoot;
    private TMP_Text statusText;
    private GameObject hoverTooltip;
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
        IUiClock uiClock)
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
        DungeonUiThemeRuntime.Ensure(canvas, fontService, uiClock).ApplyNow();
        Render();
    }

    public void Dispose()
    {
        preparationService.Changed -= HandlePreparationChanged;
        preparationService.Cancel();
        HideTraitTooltip();
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
        HideTraitTooltip();
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
        RenderMemberDetail(detail);

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

    private void RenderMemberDetail(Transform parent)
    {
        StartPartyMemberPreparation member = ResolveSelectedMember();
        if (member == null)
        {
            TMP_Text empty = CreateText(parent, "NoMember", "\uC900\uBE44 \uC911\uC778 \uCE90\uB9AD\uD130\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.", 22f, TextAlignmentOptions.Center);
            Stretch(empty.rectTransform);
            return;
        }

        Image portraitFrame = CreateImage(parent, "MemberPortraitFrame", DungeonUiTheme.SurfaceRaised);
        SetRect(portraitFrame.rectTransform, new Vector2(0.045f, 0.755f), new Vector2(0.17f, 0.955f));
        Image portrait = CreateImage(parent, "MemberPortrait", Color.clear);
        portrait.transform.SetParent(portraitFrame.transform, false);
        Stretch(portrait.rectTransform, new Vector2(8f, 8f), new Vector2(-8f, -8f));
        portrait.sprite = member.CharacterData != null ? member.CharacterData.characterSprite : null;
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        TMP_Text name = CreateText(parent, "MemberName", ResolveMemberName(member), 32f, TextAlignmentOptions.MidlineLeft);
        SetRect(name.rectTransform, new Vector2(0.195f, 0.895f), new Vector2(0.62f, 0.965f));
        name.fontStyle = FontStyles.Bold;

        string subtitle = $"{member.RosterLabel}  ·  {member.CharacterData?.SpeciesTag ?? "-"}  ·  Lv.{member.Progression?.Level ?? 1}";
        TMP_Text sub = CreateText(parent, "MemberSubtitle", subtitle, 17f, TextAlignmentOptions.MidlineLeft);
        SetRect(sub.rectTransform, new Vector2(0.197f, 0.845f), new Vector2(0.62f, 0.895f));
        sub.color = DungeonUiTheme.TextSecondary;

        TMP_Text role = CreateText(parent, "MemberRole", GetMemberReadyLabel(member), 17f, TextAlignmentOptions.MidlineRight);
        SetRect(role.rectTransform, new Vector2(0.62f, 0.895f), new Vector2(0.885f, 0.965f));
        role.color = DungeonUiTheme.TextSecondary;

        if (!member.IsOwner)
        {
            CreateDiceButton(
                parent,
                "PreparationFullRerollDice_" + member.Index,
                () => Reroll(member, null),
                new Vector2(0.905f, 0.89f),
                new Vector2(0.955f, 0.955f),
                member.IdentityRerollsRemaining + member.AptitudeRerollsRemaining + member.SkillRerollsRemaining > 0,
                "\uC804\uCCB4 \uB9AC\uB864");
        }

        CreateTab(parent, member, DetailTab.Identity, "\uC815\uCCB4\uC131", 0.195f);
        CreateTab(parent, member, DetailTab.Aptitude, "\uC7AC\uB2A5", 0.345f);
        CreateTab(parent, member, DetailTab.Skill, "\uC2A4\uD0AC", 0.495f);

        switch (selectedTab)
        {
            case DetailTab.Identity:
                RenderIdentityDetail(member, parent);
                break;
            case DetailTab.Aptitude:
                RenderAptitudeDetail(member, parent);
                break;
            case DetailTab.Skill:
                RenderSkillDetail(member, parent);
                break;
        }
    }

    private void CreateTab(
        Transform parent,
        StartPartyMemberPreparation member,
        DetailTab tab,
        string label,
        float left)
    {
        Button button = CreateButton(
            parent,
            $"PreparationTab_{member.Index}_{tab}",
            label,
            () =>
            {
                selectedTab = tab;
                Render();
            },
            new Vector2(left, 0.78f),
            new Vector2(left + 0.13f, 0.85f),
            selectedTab == tab);
        button.image.color = selectedTab == tab
            ? DungeonUiTheme.Accent
            : DungeonUiTheme.SurfaceRaised;
    }

    private void RenderIdentityDetail(StartPartyMemberPreparation member, Transform parent)
    {
        CharacterGrowthState growth = member.Progression?.GrowthState;
        Transform basics = CreatePanel(parent, "IdentityBasics", new Vector2(0.045f, 0.38f), new Vector2(0.46f, 0.72f), false);
        basics.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;
        CreateSectionTitle(basics, "\uAE30\uBCF8 \uC815\uBCF4", member, StartPartyRerollGroup.Identity);
        CreateInfoRow(basics, "\uC774\uB984", ResolveMemberName(member), 0.63f);
        CreateInfoRow(basics, "\uC5ED\uD560", member.RosterLabel, 0.46f);
        CreateInfoRow(basics, "\uC885\uC871", member.CharacterData?.SpeciesTag ?? "-", 0.29f);
        CreateInfoRow(basics, "\uCD9C\uC2E0", growth?.origin ?? "-", 0.12f);

        Transform traits = CreatePanel(parent, "IdentityTraits", new Vector2(0.485f, 0.38f), new Vector2(0.955f, 0.72f), false);
        traits.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;
        TMP_Text traitTitle = CreateText(traits, "TraitTitle", "\uD2B9\uC131", 20f, TextAlignmentOptions.MidlineLeft);
        SetRect(traitTitle.rectTransform, new Vector2(0.05f, 0.78f), new Vector2(0.65f, 0.94f));
        traitTitle.fontStyle = FontStyles.Bold;

        IReadOnlyList<CharacterTraitSO> resolvedTraits = member.Progression?.ResolveSelectedTraits()
            ?? Array.Empty<CharacterTraitSO>();
        if (resolvedTraits.Count == 0)
        {
            TMP_Text none = CreateText(traits, "TraitNone", "-", 17f, TextAlignmentOptions.TopLeft);
            SetRect(none.rectTransform, new Vector2(0.05f, 0.1f), new Vector2(0.94f, 0.73f));
            none.color = DungeonUiTheme.TextSecondary;
            return;
        }

        for (int i = 0; i < resolvedTraits.Count && i < 3; i++)
        {
            RenderTraitChip(traits, resolvedTraits[i], 0.56f - i * 0.25f);
        }
    }

    private void RenderAptitudeDetail(StartPartyMemberPreparation member, Transform parent)
    {
        CharacterGrowthState growth = member.Progression?.GrowthState;
        Transform summary = CreatePanel(parent, "AptitudeSummary", new Vector2(0.045f, 0.57f), new Vector2(0.955f, 0.72f), false);
        summary.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;
        CreateSectionTitle(summary, "\uC7AC\uB2A5", member, StartPartyRerollGroup.Aptitude);
        TMP_Text potential = CreateText(
            summary,
            "PotentialValue",
            $"\uC7A0\uC7AC\uB825  {PotentialLabel(growth?.potentialGrade ?? CharacterPotentialGrade.Ordinary)}",
            22f,
            TextAlignmentOptions.MidlineLeft);
        SetRect(potential.rectTransform, new Vector2(0.04f, 0.22f), new Vector2(0.5f, 0.68f));
        potential.color = DungeonUiTheme.Accent;
        potential.fontStyle = FontStyles.Bold;

        int total = Enum.GetValues(typeof(CharacterStatType))
            .Cast<CharacterStatType>()
            .Sum(type => growth?.initialBaseStats?.Get(type) ?? 0);
        TMP_Text totalText = CreateText(summary, "StatTotal", $"\uCD08\uAE30 \uB2A5\uB825\uCE58 \uD569\uACC4  {total}", 17f, TextAlignmentOptions.MidlineRight);
        SetRect(totalText.rectTransform, new Vector2(0.5f, 0.22f), new Vector2(0.94f, 0.68f));
        totalText.color = DungeonUiTheme.TextSecondary;

        Transform stats = CreatePanel(parent, "AptitudeStats", new Vector2(0.045f, 0.08f), new Vector2(0.955f, 0.54f), false);
        stats.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;
        CharacterStatType[] types = Enum.GetValues(typeof(CharacterStatType)).Cast<CharacterStatType>().ToArray();
        for (int i = 0; i < types.Length; i++)
        {
            int column = i / 3;
            int row = i % 3;
            float left = 0.04f + column * 0.315f;
            float top = 0.82f - row * 0.28f;
            RenderStatBar(stats, types[i], growth?.initialBaseStats?.Get(types[i]) ?? 0, left, top);
        }
    }

    private void RenderSkillDetail(StartPartyMemberPreparation member, Transform parent)
    {
        Transform slots = CreatePanel(parent, "SkillSlots", new Vector2(0.045f, 0.38f), new Vector2(0.955f, 0.72f), false);
        slots.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;
        CreateSectionTitle(slots, "\uC2A4\uD0AC", member, StartPartyRerollGroup.Skill);

        if (member.IsOwner)
        {
            IReadOnlyList<CharacterSkillInstance> ownerSkills = CharacterOwnerFixedSkillUtility.GetSkills(member.CharacterData);
            for (int i = 0; i < ownerSkills.Count && i < CharacterOwnerFixedSkillUtility.FixedSlotCount; i++)
            {
                RenderSkillCard(slots, ownerSkills[i], i, 0.08f + i * 0.225f, 0.12f, 0.205f, true);
            }

            TMP_Text hint = CreateText(
                parent,
                "OwnerSkillHint",
                "\uC0AC\uC7A5\uC740 \uACE0\uC815 \uAD8C\uB2A5\uC73C\uB85C \uB7F0\uC744 \uC2DC\uC791\uD569\uB2C8\uB2E4. \uC77C\uBC18 \uC131\uC7A5 \uC2A4\uD0AC\uC740 \uB7F0 \uC911 \uD574\uAE08\uB429\uB2C8\uB2E4.",
                18f,
                TextAlignmentOptions.MidlineLeft);
            SetRect(hint.rectTransform, new Vector2(0.055f, 0.25f), new Vector2(0.94f, 0.34f));
            hint.color = DungeonUiTheme.TextSecondary;
            hint.textWrappingMode = TextWrappingModes.Normal;
            return;
        }

        RenderSlotSummary(slots, "\uC885\uC871 \uC561\uD2F0\uBE0C", member.CharacterData?.SpeciesTag ?? "-", 0.64f);
        RenderSlotSummary(slots, "\uCD08\uAE30 \uC561\uD2F0\uBE0C", member.Progression.ActiveSkills.FirstOrDefault()?.displayName ?? "\uC0DD\uC131 \uD544\uC694", 0.43f);
        RenderSlotSummary(slots, "\uD328\uC2DC\uBE0C", member.Progression.PassiveSkills.FirstOrDefault()?.displayName ?? "\uC0DD\uC131 \uD544\uC694", 0.22f);

        Transform generated = CreatePanel(parent, "GeneratedStartSkills", new Vector2(0.045f, 0.08f), new Vector2(0.955f, 0.35f), false);
        generated.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;
        RenderGeneratedStartSkills(member, generated);
    }

    private void RenderGeneratedStartSkills(StartPartyMemberPreparation member, Transform parent)
    {
        TMP_Text title = CreateText(parent, "GeneratedSkillTitle", "\uCD08\uAE30 \uC2A4\uD0AC", 18f, TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(0.035f, 0.76f), new Vector2(0.55f, 0.95f));
        title.fontStyle = FontStyles.Bold;

        CharacterSkillInstance active = member.Progression?.ActiveSkills.FirstOrDefault();
        CharacterSkillInstance passive = member.Progression?.PassiveSkills.FirstOrDefault();
        if (active == null || passive == null)
        {
            TMP_Text waiting = CreateText(parent, "SkillGenerationMissing", "\uCD08\uAE30 \uC2A4\uD0AC\uC744 \uB2E4\uC2DC \uAD6C\uC131\uD558\uACE0 \uC788\uC2B5\uB2C8\uB2E4.", 18f, TextAlignmentOptions.MidlineLeft);
            SetRect(waiting.rectTransform, new Vector2(0.035f, 0.2f), new Vector2(0.8f, 0.68f));
            waiting.color = DungeonUiTheme.Warning;
            return;
        }

        TMP_Text hint = CreateText(parent, "GeneratedSkillHint", "\uCCAB \uC561\uD2F0\uBE0C\uB294 \uC120\uD0DD\uD558\uC9C0 \uC54A\uACE0 \uC815\uCCB4\uC131\uACFC \uC7AC\uB2A5\uC5D0 \uB9DE\uAC8C \uC989\uC2DC \uD655\uC815\uB429\uB2C8\uB2E4.", 14f, TextAlignmentOptions.MidlineRight);
        SetRect(hint.rectTransform, new Vector2(0.48f, 0.76f), new Vector2(0.955f, 0.95f));
        hint.color = DungeonUiTheme.TextSecondary;
        hint.textWrappingMode = TextWrappingModes.Normal;

        RenderSkillCard(parent, active, 0, 0.05f, 0.12f, 0.42f, false);
        RenderSkillCard(parent, passive, 1, 0.53f, 0.12f, 0.42f, false);
    }

    private void RenderRerollButtons(StartPartyMemberPreparation member, Transform parent)
    {
        CreateButton(parent, "PreparationFullReroll_" + member.Index, "\uC804\uCCB4 \uB9AC\uB864", () => Reroll(member, null),
            new Vector2(0.045f, 0.04f), new Vector2(0.18f, 0.1f));
        CreateButton(parent, "PreparationIdentityReroll_" + member.Index, $"\uC815\uCCB4\uC131 {member.IdentityRerollsRemaining}", () => Reroll(member, StartPartyRerollGroup.Identity),
            new Vector2(0.195f, 0.04f), new Vector2(0.34f, 0.1f));
        CreateButton(parent, "PreparationAptitudeReroll_" + member.Index, $"\uC7AC\uB2A5 {member.AptitudeRerollsRemaining}", () => Reroll(member, StartPartyRerollGroup.Aptitude),
            new Vector2(0.355f, 0.04f), new Vector2(0.5f, 0.1f));
        CreateButton(parent, "PreparationSkillReroll_" + member.Index, $"\uC2A4\uD0AC {member.SkillRerollsRemaining}", () => Reroll(member, StartPartyRerollGroup.Skill),
            new Vector2(0.515f, 0.04f), new Vector2(0.66f, 0.1f));
    }

    private void RenderSwapButtons(StartPartyMemberPreparation member, Transform parent)
    {
        CreateButton(parent, "PreparationSwap_" + member.Index + "_1", "\uC9C1\uC6D0 1\uACFC \uAD50\uCCB4", () => Swap(member, 1),
            new Vector2(0.69f, 0.04f), new Vector2(0.82f, 0.1f));
        CreateButton(parent, "PreparationSwap_" + member.Index + "_2", "\uC9C1\uC6D0 2\uC640 \uAD50\uCCB4", () => Swap(member, 2),
            new Vector2(0.835f, 0.04f), new Vector2(0.965f, 0.1f));
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
        selectedTab = DetailTab.Identity;
        SetStatus(message, false);
        Render();
    }

    private void BackToOwnerSelect()
    {
        preparationService.Cancel();
        screen = PreparationScreen.OwnerSelect;
        selectedTab = DetailTab.Identity;
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

    private void CreateSectionTitle(
        Transform parent,
        string label,
        StartPartyMemberPreparation member,
        StartPartyRerollGroup? rerollGroup)
    {
        TMP_Text title = CreateText(parent, "SectionTitle", label, 20f, TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(0.045f, 0.78f), new Vector2(0.7f, 0.95f));
        title.fontStyle = FontStyles.Bold;
        if (member == null || member.IsOwner || !rerollGroup.HasValue)
        {
            return;
        }

        int remaining = rerollGroup.Value switch
        {
            StartPartyRerollGroup.Identity => member.IdentityRerollsRemaining,
            StartPartyRerollGroup.Aptitude => member.AptitudeRerollsRemaining,
            StartPartyRerollGroup.Skill => member.SkillRerollsRemaining,
            _ => 0
        };
        CreateDiceButton(
            parent,
            $"Preparation{rerollGroup.Value}RerollDice_{member.Index}",
            () => Reroll(member, rerollGroup.Value),
            new Vector2(0.875f, 0.76f),
            new Vector2(0.955f, 0.94f),
            remaining > 0,
            $"{label} \uB9AC\uB864 {remaining}");
    }

    private void CreateInfoRow(Transform parent, string label, string value, float bottom)
    {
        TMP_Text labelText = CreateText(parent, "InfoLabel_" + label, label, 15f, TextAlignmentOptions.MidlineLeft);
        SetRect(labelText.rectTransform, new Vector2(0.05f, bottom), new Vector2(0.27f, bottom + 0.13f));
        labelText.color = DungeonUiTheme.TextSecondary;
        TMP_Text valueText = CreateText(parent, "InfoValue_" + label, value, 17f, TextAlignmentOptions.MidlineLeft);
        SetRect(valueText.rectTransform, new Vector2(0.28f, bottom), new Vector2(0.94f, bottom + 0.13f));
        valueText.textWrappingMode = TextWrappingModes.Normal;
    }

    private void RenderTraitChip(Transform parent, CharacterTraitSO trait, float bottom)
    {
        Transform chip = CreatePanel(parent, "TraitChip_" + (trait != null ? trait.id : 0), new Vector2(0.05f, bottom), new Vector2(0.94f, bottom + 0.2f), false);
        chip.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.03f, 0.85f);
        TMP_Text title = CreateText(chip, "TraitName", trait != null ? trait.traitName : "-", 17f, TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(0.04f, 0.58f), new Vector2(0.95f, 0.95f));
        title.color = DungeonUiTheme.Accent;
        title.fontStyle = FontStyles.Bold;
        TMP_Text description = CreateText(chip, "TraitDescription", trait != null ? trait.description : string.Empty, 14f, TextAlignmentOptions.TopLeft);
        SetRect(description.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.95f, 0.55f));
        description.color = DungeonUiTheme.TextSecondary;
        description.textWrappingMode = TextWrappingModes.Normal;

        EventTrigger trigger = chip.gameObject.AddComponent<EventTrigger>();
        AddEventTrigger(trigger, EventTriggerType.PointerEnter, () => ShowTraitTooltip(trait));
        AddEventTrigger(trigger, EventTriggerType.PointerExit, HideTraitTooltip);
    }

    private void ShowTraitTooltip(CharacterTraitSO trait)
    {
        if (trait == null || root == null)
        {
            return;
        }

        HideTraitTooltip();
        hoverTooltip = new GameObject("TraitStatTooltip", typeof(RectTransform), typeof(Image));
        hoverTooltip.transform.SetParent(root.transform, false);
        RectTransform rect = hoverTooltip.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.57f, 0.11f), new Vector2(0.955f, 0.34f));
        Image panel = hoverTooltip.GetComponent<Image>();
        panel.color = new Color(0.02f, 0.025f, 0.03f, 0.97f);
        panel.raycastTarget = false;

        TMP_Text title = CreateText(hoverTooltip.transform, "TooltipTitle", trait.traitName, 19f, TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(0.045f, 0.74f), new Vector2(0.95f, 0.94f));
        title.color = DungeonUiTheme.Accent;
        title.fontStyle = FontStyles.Bold;

        TMP_Text body = CreateText(hoverTooltip.transform, "TooltipBody", BuildTraitTooltipText(trait), 14f, TextAlignmentOptions.TopLeft);
        SetRect(body.rectTransform, new Vector2(0.045f, 0.08f), new Vector2(0.95f, 0.72f));
        body.color = DungeonUiTheme.TextPrimary;
        body.textWrappingMode = TextWrappingModes.Normal;
        hoverTooltip.transform.SetAsLastSibling();
    }

    private void HideTraitTooltip()
    {
        if (hoverTooltip == null)
        {
            return;
        }

        UnityEngine.Object.Destroy(hoverTooltip);
        hoverTooltip = null;
    }

    private static void AddEventTrigger(EventTrigger trigger, EventTriggerType eventId, Action action)
    {
        if (trigger == null)
        {
            return;
        }

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventId };
        entry.callback.AddListener(_ => action?.Invoke());
        trigger.triggers.Add(entry);
    }

    private static string BuildTraitTooltipText(CharacterTraitSO trait)
    {
        StringBuilder builder = new StringBuilder();
        List<string> statLines = new List<string>();
        foreach (CharacterStatType type in Enum.GetValues(typeof(CharacterStatType)).Cast<CharacterStatType>())
        {
            int value = trait.statBonus?.Get(type) ?? 0;
            if (value != 0)
            {
                statLines.Add($"{StatLabel(type)} {value:+#;-#;0}");
            }
        }

        builder.AppendLine(statLines.Count > 0
            ? "스탯 변화  " + string.Join(" · ", statLines)
            : "스탯 변화  없음");

        CharacterModelModifiers modifiers = trait.modifiers;
        List<string> modifierLines = new List<string>();
        AddMultiplierLine(modifierLines, "욕구 소모", modifiers?.consumptionMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "소비 성향", modifiers?.spendingMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "대기 인내", modifiers?.waitPatienceMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "혼잡 민감도", modifiers?.crowdSensitivityMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "사고 확률", modifiers?.accidentChanceMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "작업 속도", modifiers?.workSpeedMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "연구 속도", modifiers?.researchSpeedMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "전투력", modifiers?.combatPowerMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "이동 속도", modifiers?.moveSpeedMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "체류 시간", modifiers?.stayDurationMultiplier ?? 1f);
        if (modifierLines.Count > 0)
        {
            builder.AppendLine("세부 보정  " + string.Join(" · ", modifierLines));
        }

        AppendFlags(builder, "선호 시설", FormatFacilityRoles(modifiers?.preferredFacilityRoles ?? FacilityRole.None));
        AppendFlags(builder, "기피 시설", FormatFacilityRoles(modifiers?.dislikedFacilityRoles ?? FacilityRole.None));
        AppendFlags(
            builder,
            "선호 업무",
            FormatWorkTypes(modifiers != null ? modifiers.PreferredWorkTypeIds : Array.Empty<WorkTypeId>()));
        AppendFlags(
            builder,
            "기피 업무",
            FormatWorkTypes(modifiers != null ? modifiers.DislikedWorkTypeIds : Array.Empty<WorkTypeId>()));
        return builder.ToString().TrimEnd();
    }

    private static void AddMultiplierLine(List<string> lines, string label, float value)
    {
        if (Mathf.Approximately(value, 1f))
        {
            return;
        }

        float delta = (value - 1f) * 100f;
        lines.Add($"{label} x{value:0.##} ({delta:+0.#;-0.#;0}%)");
    }

    private static void AppendFlags(StringBuilder builder, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"{label}  {value}");
        }
    }

    private static string FormatFacilityRoles(FacilityRole roles)
    {
        if (roles == FacilityRole.None)
        {
            return string.Empty;
        }

        return string.Join(", ", Enum.GetValues(typeof(FacilityRole))
            .Cast<FacilityRole>()
            .Where(role => role != FacilityRole.None && (roles & role) != 0)
            .Select(FacilityRoleLabel));
    }

    private static string FormatWorkTypes(IEnumerable<WorkTypeId> workTypeIds)
    {
        return CodexTextFormatter.FormatWorkTypes(workTypeIds);
    }

    private static string FacilityRoleLabel(FacilityRole role)
    {
        return role switch
        {
            FacilityRole.Meal => "식사",
            FacilityRole.Purchase => "구매",
            FacilityRole.Rest => "휴식",
            FacilityRole.Training => "훈련",
            FacilityRole.Research => "연구",
            FacilityRole.Mana => "마나",
            FacilityRole.Logistics => "물류",
            FacilityRole.Toilet => "화장실",
            FacilityRole.Hygiene => "위생",
            FacilityRole.Administration => "운영",
            FacilityRole.Security => "방어",
            _ => role.ToString()
        };
    }


    private void RenderStatBar(Transform parent, CharacterStatType type, int value, float left, float top)
    {
        TMP_Text label = CreateText(parent, "StatLabel_" + type, StatLabel(type), 15f, TextAlignmentOptions.MidlineLeft);
        SetRect(label.rectTransform, new Vector2(left, top - 0.12f), new Vector2(left + 0.13f, top));
        label.color = DungeonUiTheme.TextSecondary;

        TMP_Text number = CreateText(parent, "StatValue_" + type, value.ToString(), 16f, TextAlignmentOptions.MidlineRight);
        SetRect(number.rectTransform, new Vector2(left + 0.25f, top - 0.12f), new Vector2(left + 0.285f, top));

        Image back = CreateImage(parent, "StatBack_" + type, new Color(0.02f, 0.025f, 0.03f, 0.9f));
        SetRect(back.rectTransform, new Vector2(left + 0.14f, top - 0.085f), new Vector2(left + 0.245f, top - 0.035f));
        Image fill = CreateImage(back.transform, "Fill", DungeonUiTheme.Accent);
        float normalized = Mathf.Clamp01(value / 10f);
        SetRect(fill.rectTransform, Vector2.zero, new Vector2(normalized, 1f));
    }

    private void RenderSlotSummary(Transform parent, string label, string value, float bottom)
    {
        Transform row = CreatePanel(parent, "SkillSlot_" + label, new Vector2(0.05f, bottom), new Vector2(0.95f, bottom + 0.16f), false);
        row.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.03f, 0.85f);
        TMP_Text labelText = CreateText(row, "Label", label, 15f, TextAlignmentOptions.MidlineLeft);
        SetRect(labelText.rectTransform, new Vector2(0.035f, 0.12f), new Vector2(0.3f, 0.88f));
        labelText.color = DungeonUiTheme.TextSecondary;
        TMP_Text valueText = CreateText(row, "Value", value, 18f, TextAlignmentOptions.MidlineLeft);
        SetRect(valueText.rectTransform, new Vector2(0.32f, 0.12f), new Vector2(0.95f, 0.88f));
    }

    private void RenderSkillCard(
        Transform parent,
        CharacterSkillInstance skill,
        int index,
        float left,
        float bottom,
        float width,
        bool fixedSkill)
    {
        Transform card = CreatePanel(parent, "OwnerSkillCard_" + index, new Vector2(left, bottom), new Vector2(left + width, bottom + 0.52f), false);
        card.GetComponent<Image>().color = fixedSkill
            ? new Color(DungeonUiTheme.Accent.r, DungeonUiTheme.Accent.g, DungeonUiTheme.Accent.b, 0.25f)
            : new Color(0.02f, 0.025f, 0.03f, 0.85f);
        TMP_Text name = CreateText(card, "Name", skill != null ? skill.displayName : "-", 16f, TextAlignmentOptions.TopLeft);
        SetRect(name.rectTransform, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.92f));
        name.fontStyle = FontStyles.Bold;
        name.textWrappingMode = TextWrappingModes.Normal;
        TMP_Text desc = CreateText(card, "Description", skill != null ? skill.description : string.Empty, 13f, TextAlignmentOptions.TopLeft);
        SetRect(desc.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.55f));
        desc.color = DungeonUiTheme.TextSecondary;
        desc.textWrappingMode = TextWrappingModes.Normal;
    }

    private string BuildRosterLabel(StartPartyMemberPreparation member)
    {
        string state = member.IsReadyToStart ? "\uC900\uBE44 \uC644\uB8CC" : "\uC900\uBE44 \uC911";
        return $"{member.RosterLabel}\n{ResolveMemberName(member)} - {state}";
    }

    private string ResolveMemberName(StartPartyMemberPreparation member)
    {
        string preparedName = member.Progression?.GrowthState?.displayName;
        if (!string.IsNullOrWhiteSpace(preparedName))
        {
            return preparedName;
        }

        return member.CharacterData != null ? member.CharacterData.characterName : "-";
    }

    private string BuildDetailText(StartPartyMemberPreparation member)
    {
        CharacterGrowthState growth = member.Progression?.GrowthState;
        if (growth == null)
        {
            return "\uC900\uBE44 \uC911\uC785\uB2C8\uB2E4.";
        }

        return selectedTab switch
        {
            DetailTab.Identity => $"\uCD9C\uC2E0\n{growth.origin}\n\n\uD2B9\uC131\n{FormatTraitIds(growth.traitIds)}\n\n\uC885\uC871\n{member.CharacterData?.SpeciesTag}",
            DetailTab.Aptitude => $"\uC7A0\uC7AC\uB825\n{growth.potentialGrade}\n\n\uC2A4\uD0EF\n{FormatStats(growth.initialBaseStats)}",
            DetailTab.Skill => BuildSkillText(member),
            _ => string.Empty
        };
    }

    private string BuildSkillText(StartPartyMemberPreparation member)
    {
        string fixedSkills = member.IsOwner
            ? "\uC0AC\uC7A5 \uACE0\uC815\n" + string.Join("\n", CharacterOwnerFixedSkillUtility.GetSkills(member.CharacterData).Select(skill => "- " + skill.displayName)) + "\n\n"
            : string.Empty;
        string active = member.Progression.ActiveSkills.Count > 0
            ? member.Progression.ActiveSkills[0].displayName
            : "\uC120\uD0DD \uC804";
        string passive = member.Progression.PassiveSkills.Count > 0
            ? member.Progression.PassiveSkills[0].displayName
            : "\uC900\uBE44 \uC911";
        CharacterSkillSlotProfile profile = member.SlotProfile;
        return $"{fixedSkills}\uC2AC\uB86F\n\uC885\uC871 {profile.SpeciesActiveSlots} - \uC561\uD2F0\uBE0C {profile.NormalActiveSlots} - \uD328\uC2DC\uBE0C {profile.PassiveSlots} - \uAD81\uADF9\uAE30 {profile.UltimateSlots}\n\n\uCCAB \uC561\uD2F0\uBE0C\n{active}\n\n\uCCAB \uD328\uC2DC\uBE0C\n{passive}";
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

    private static string FormatTraitIds(IEnumerable<int> traitIds)
    {
        int[] ids = traitIds?.Distinct().ToArray() ?? Array.Empty<int>();
        return ids.Length == 0 ? "-" : string.Join(", ", ids.Select(id => $"Trait {id}"));
    }

    private static string FormatStats(CharacterStatBlock stats)
    {
        if (stats == null)
        {
            return "-";
        }

        return string.Join(" - ", Enum.GetValues(typeof(CharacterStatType))
            .Cast<CharacterStatType>()
            .Select(type => $"{type} {stats.Get(type)}"));
    }

    private static string PotentialLabel(CharacterPotentialGrade grade)
    {
        return grade switch
        {
            CharacterPotentialGrade.Promising => "\uC720\uB9DD",
            CharacterPotentialGrade.Excellent => "\uC6B0\uC218",
            CharacterPotentialGrade.Exceptional => "\uD0C1\uC6D4",
            CharacterPotentialGrade.Genius => "\uCC9C\uC7AC",
            _ => "\uD3C9\uBC94"
        };
    }

    private static string StatLabel(CharacterStatType type)
    {
        return type switch
        {
            CharacterStatType.Attack => "\uACF5\uACA9",
            CharacterStatType.Sales => "\uD310\uB9E4",
            CharacterStatType.Research => "\uC5F0\uAD6C",
            CharacterStatType.MoveSpeed => "\uC774\uB3D9",
            CharacterStatType.Strength => "\uADFC\uB825",
            CharacterStatType.Toughness => "\uB9F7\uC9D1",
            CharacterStatType.Dexterity => "\uBBFC\uCCA9",
            CharacterStatType.Cleaning => "\uCCAD\uC18C",
            CharacterStatType.Endurance => "\uC9C0\uAD6C",
            _ => type.ToString()
        };
    }

    private Button CreateDiceButton(
        Transform parent,
        string name,
        Action clicked,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool interactable,
        string accessibleLabel)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        SetRect(rect, anchorMin, anchorMax);
        Image image = obj.GetComponent<Image>();
        image.color = interactable ? DungeonUiTheme.Surface : DungeonUiTheme.SurfaceMuted;

        Button button = obj.GetComponent<Button>();
        button.interactable = interactable;
        button.onClick.AddListener(() => clicked?.Invoke());
        CreateDiceDot(obj.transform, "DotA", new Vector2(0.28f, 0.28f), interactable);
        CreateDiceDot(obj.transform, "DotB", new Vector2(0.72f, 0.28f), interactable);
        CreateDiceDot(obj.transform, "DotC", new Vector2(0.5f, 0.5f), interactable);
        CreateDiceDot(obj.transform, "DotD", new Vector2(0.28f, 0.72f), interactable);
        CreateDiceDot(obj.transform, "DotE", new Vector2(0.72f, 0.72f), interactable);

        TMP_Text hidden = CreateText(obj.transform, "AccessibleLabel", accessibleLabel, 1f, TextAlignmentOptions.Center);
        Stretch(hidden.rectTransform);
        hidden.color = Color.clear;
        hidden.raycastTarget = false;
        return button;
    }

    private void CreateDiceDot(Transform parent, string name, Vector2 anchor, bool enabled)
    {
        Image dot = CreateImage(parent, name, enabled ? DungeonUiTheme.TextPrimary : DungeonUiTheme.TextSecondary);
        RectTransform rect = dot.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(5f, 5f);
        rect.anchoredPosition = Vector2.zero;
        dot.raycastTarget = false;
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

    private TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        float size,
        TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TMP_Text label = obj.GetComponent<TMP_Text>();
        label.text = text ?? string.Empty;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = DungeonUiTheme.TextPrimary;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.font = fontService.Resolve();
        return label;
    }

    private Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Transform CreatePanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool raised)
    {
        Image panel = CreateImage(parent, name, raised ? DungeonUiTheme.Surface : DungeonUiTheme.SurfaceMuted);
        SetRect(panel.rectTransform, anchorMin, anchorMax);
        panel.raycastTarget = true;
        return panel.transform;
    }

    private Button CreateButton(
        Transform parent,
        string name,
        string label,
        Action clicked,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool selected = false)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        SetRect(rect, anchorMin, anchorMax);
        Image image = obj.GetComponent<Image>();
        image.color = selected ? DungeonUiTheme.Accent : DungeonUiTheme.SurfaceRaised;

        Button button = obj.GetComponent<Button>();
        button.onClick.AddListener(() => clicked?.Invoke());

        TMP_Text text = CreateText(obj.transform, "Label", label, 17f, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, new Vector2(10f, 4f), new Vector2(-10f, -4f));
        text.textWrappingMode = TextWrappingModes.Normal;
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        Stretch(rect, Vector2.zero, Vector2.zero);
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

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
