using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using static CharacterSummaryRuntimeLayout;

public sealed class CharacterSummaryRuntimeLogFactory : ICharacterSummaryRuntimeLogFactory
{
    private const string RuntimeViewName = "CharacterSummaryGeneratedView";

    private readonly ITmpKoreanFontService tmpKoreanFontService;

    [Inject]
    public CharacterSummaryRuntimeLogFactory(ITmpKoreanFontService tmpKoreanFontService)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
    }

    public void Ensure(
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

        ConfigurePanelBounds(uiRoot);

        Transform generated = uiRoot.transform.Find(RuntimeViewName);
        if (generated != null
            && (generated.Find("TabBar/GrowthTab") == null
                || generated.Find("TabBar/HealthTab") == null
                || generated.Find("TabBar/CombatTab") == null
                || generated.Find("TabBar/AiTab") == null
                || generated.Find("Content/GrowthContent/GrowthList") == null
                || generated.Find("Content/StatusContent/Thirst") == null
                || generated.Find("Content/HealthContent/HealthContentViewport/HealthSummaryText") == null
                || generated.Find("Content/HealthContent/HealthCommandRow/CaptivityCommand") == null
                || generated.Find("Content/HealthContent/HealthCommandRow/DietPolicy") == null
                || generated.Find("Content/HealthContent/HealthCommandRow/SurgeryCommand") == null
                || generated.Find("Content/HealthContent/HealthCommandRow/AutomaticSurgery") == null
                || generated.Find("Content/HealthContent/SubstanceCommandRow/SubstanceSelection") == null
                || generated.Find("Content/HealthContent/SubstanceCommandRow/SubstancePolicy") == null
                || generated.Find("Content/CombatContent/CombatContentViewport/CombatSummaryText") == null
                || generated.Find("Content/CombatContent/CombatCommands/LoadoutButton") == null
                || generated.Find("Content/StatusContent/CarrySummaryText") == null
                || generated.Find("Content/AiContent/AiContentViewport/AiSummaryText") == null
                || generated.Find("Header/DetailedStatsButton") == null
                || generated.Find("DetailedOverlay/DetailedViewport/DetailedStatsText") == null))
        {
            UnityEngine.Object.DestroyImmediate(generated.gameObject);
            generated = null;
        }

        if (generated == null)
        {
            DisableLegacyChildren(uiRoot.transform);
            generated = CreateView(view, actions, uiRoot.transform);
        }

        Bind(view, generated);
        ApplyFonts(uiRoot.transform);
    }

    public void ApplyFonts(Transform root)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        tmpKoreanFontService.ApplyToChildren(root);
    }

    private Transform CreateView(
        ICharacterSummaryGeneratedView viewBinding,
        CharacterSummaryViewActions actions,
        Transform parent)
    {
        RectTransform view = CreateRect(RuntimeViewName, parent);
        SetStretch(view, Vector2.zero, Vector2.zero);

        RectTransform header = CreateRect("Header", view);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = Vector2.zero;
        header.sizeDelta = new Vector2(0f, 76f);
        header.gameObject.AddComponent<Image>().color = DungeonUiTheme.SurfaceRaised;

        TMP_Text nameText = CreateText("CharacterName", header, 28f, FontStyles.Bold);
        nameText.alignment = TextAlignmentOptions.BottomLeft;
        nameText.color = DungeonUiTheme.TextPrimary;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 18f;
        nameText.fontSizeMax = 28f;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Truncate;
        SetStretch(nameText.rectTransform, new Vector2(18f, 32f), new Vector2(-168f, -8f));

        TMP_Text profileText = CreateText("CharacterProfile", header, 15f, FontStyles.Normal);
        profileText.alignment = TextAlignmentOptions.TopLeft;
        profileText.color = DungeonUiTheme.TextSecondary;
        profileText.textWrappingMode = TextWrappingModes.NoWrap;
        profileText.overflowMode = TextOverflowModes.Truncate;
        SetStretch(profileText.rectTransform, new Vector2(18f, 8f), new Vector2(-168f, -44f));

        Button detailedStatsButton = CreateButton(
            "DetailedStatsButton",
            header,
            "!");
        RectTransform detailedButtonRect = detailedStatsButton.GetComponent<RectTransform>();
        detailedButtonRect.anchorMin = new Vector2(1f, 1f);
        detailedButtonRect.anchorMax = new Vector2(1f, 1f);
        detailedButtonRect.pivot = new Vector2(1f, 1f);
        detailedButtonRect.anchoredPosition = new Vector2(-88f, -12f);
        detailedButtonRect.sizeDelta = new Vector2(68f, 36f);
        detailedStatsButton.onClick.AddListener(actions.Popup.OpenDetailedStats);

        Button closeButton = CreateButton(
            "CloseButton",
            header,
            CharacterSummaryUiTextQuery.Get("CharacterSummary.Action.Close"));
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-12f, -12f);
        closeRect.sizeDelta = new Vector2(68f, 36f);
        closeButton.onClick.AddListener(actions.Popup.RequestClose);

        RectTransform tabBar = CreateRect("TabBar", view);
        tabBar.anchorMin = new Vector2(0f, 1f);
        tabBar.anchorMax = new Vector2(1f, 1f);
        tabBar.pivot = new Vector2(0.5f, 1f);
        tabBar.anchoredPosition = new Vector2(0f, -82f);
        tabBar.sizeDelta = new Vector2(0f, 42f);
        HorizontalLayoutGroup tabs = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabs.padding = new RectOffset(14, 14, 0, 0);
        tabs.spacing = 6f;
        tabs.childAlignment = TextAnchor.MiddleLeft;
        tabs.childControlWidth = true;
        tabs.childControlHeight = true;
        tabs.childForceExpandWidth = true;
        tabs.childForceExpandHeight = true;

        Button statusTabButton = CreateTabButton("StatusTab", tabBar, CharacterSummaryUiTextQuery.Get("CharacterSummary.Tab.Status"), actions.Tabs.ShowStatus);
        Button healthTabButton = CreateTabButton("HealthTab", tabBar, CharacterSummaryUiTextQuery.Get("CharacterSummary.Tab.Health"), actions.Tabs.ShowHealth);
        Button combatTabButton = CreateTabButton("CombatTab", tabBar, CharacterSummaryUiTextQuery.Get("CharacterSummary.Tab.Combat"), actions.Tabs.ShowCombat);
        Button growthTabButton = CreateTabButton("GrowthTab", tabBar, CharacterSummaryUiTextQuery.Get("CharacterSummary.Tab.Growth"), actions.Tabs.ShowGrowth);
        Button moodTabButton = CreateTabButton("MoodTab", tabBar, CharacterSummaryUiTextQuery.Get("CharacterSummary.Tab.Mood"), actions.Tabs.ShowMood);
        Button recordsTabButton = CreateTabButton("RecordsTab", tabBar, CharacterSummaryUiTextQuery.Get("CharacterSummary.Tab.Records"), actions.Tabs.ShowRecords);
        Button aiTabButton = CreateTabButton("AiTab", tabBar, "AI", actions.Tabs.ShowAi);

        RectTransform content = CreateRect("Content", view);
        SetStretch(content, new Vector2(14f, 14f), new Vector2(-14f, -132f));

        RectTransform statusContent = CreateRect("StatusContent", content);
        SetStretch(statusContent, Vector2.zero, Vector2.zero);
        VerticalLayoutGroup vertical = statusContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 6f;
        vertical.padding = new RectOffset(0, 0, 0, 0);
        vertical.childAlignment = TextAnchor.UpperLeft;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        CreateSectionLabel(statusContent, "Status", CharacterSummaryUiTextQuery.Get("CharacterSummary.Section.Status"));
        Slider health = CreateMeterRow(statusContent, "Health", CharacterSummaryUiTextQuery.Get("CharacterSummary.Meter.Health"), 46f);
        CreateSectionLabel(statusContent, "Needs", CharacterSummaryUiTextQuery.Get("CharacterSummary.Section.Needs"));
        Slider hunger = CreateMeterRow(statusContent, "Hunger", CharacterSummaryUiTextQuery.Get("CharacterSummary.Meter.Satiety"), 40f);
        Slider thirst = CreateMeterRow(statusContent, "Thirst", CharacterSummaryUiTextQuery.Get("CharacterSummary.Meter.Thirst"), 40f);
        Slider fun = CreateMeterRow(statusContent, "Fun", CharacterSummaryUiTextQuery.Get("CharacterSummary.Meter.Fun"), 40f);
        Slider sleep = CreateMeterRow(statusContent, "Sleep", CharacterSummaryUiTextQuery.Get("CharacterSummary.Meter.Rest"), 40f);
        Slider excretion = CreateMeterRow(statusContent, "Excretion", CharacterSummaryUiTextQuery.Get("CharacterSummary.Meter.Excretion"), 40f);
        Slider hygiene = CreateMeterRow(statusContent, "Hygiene", CharacterSummaryUiTextQuery.Get("CharacterSummary.Meter.Hygiene"), 40f);

        TMP_Text carrySummary = CreateText("CarrySummaryText", statusContent, 14f, FontStyles.Normal);
        carrySummary.text = CharacterSummaryUiTextQuery.Get(
            "CharacterSummary.Carry.Empty");
        carrySummary.color = DungeonUiTheme.TextSecondary;
        carrySummary.alignment = TextAlignmentOptions.TopLeft;
        carrySummary.textWrappingMode = TextWrappingModes.Normal;
        carrySummary.overflowMode = TextOverflowModes.Ellipsis;
        carrySummary.margin = new Vector4(8f, 4f, 8f, 4f);
        LayoutElement carrySummaryLayout = carrySummary.gameObject.AddComponent<LayoutElement>();
        carrySummaryLayout.minHeight = 96f;
        carrySummaryLayout.preferredHeight = 112f;

        RectTransform healthContent = CreateRect("HealthContent", content);
        SetStretch(healthContent, Vector2.zero, Vector2.zero);
        RectTransform healthCommandRow = CreateRect(
            "HealthCommandRow",
            healthContent);
        healthCommandRow.anchorMin = new Vector2(0f, 1f);
        healthCommandRow.anchorMax = new Vector2(1f, 1f);
        healthCommandRow.pivot = new Vector2(0.5f, 1f);
        healthCommandRow.anchoredPosition = Vector2.zero;
        healthCommandRow.sizeDelta = new Vector2(0f, 44f);
        HorizontalLayoutGroup healthCommands =
            healthCommandRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        healthCommands.spacing = 5f;
        healthCommands.childControlWidth = true;
        healthCommands.childControlHeight = true;
        healthCommands.childForceExpandWidth = true;
        healthCommands.childForceExpandHeight = true;
        Button captivityCommand = CreateButton(
            "CaptivityCommand",
            healthCommandRow,
            CharacterSummaryUiTextQuery.Get(
                "CharacterSummary.Health.Action.Captivity"));
        Button dietPolicy = CreateButton(
            "DietPolicy",
            healthCommandRow,
            CharacterSummaryUiTextQuery.Get(
                "CharacterSummary.Health.Action.DietFree"));
        Button surgeryCommand = CreateButton(
            "SurgeryCommand",
            healthCommandRow,
            CharacterSummaryUiTextQuery.Get(
                "CharacterSummary.Health.Action.ScheduleSurgery"));
        Button automaticSurgery = CreateButton(
            "AutomaticSurgery",
            healthCommandRow,
            CharacterSummaryUiTextQuery.Get(
                "CharacterSummary.Health.Action.AutomaticEmergencyOn"));
        captivityCommand.onClick.AddListener(actions.Health.ExecuteCaptivityAction);
        dietPolicy.onClick.AddListener(actions.Health.CycleDietPolicy);
        surgeryCommand.onClick.AddListener(actions.Health.OpenSurgeryWindow);
        automaticSurgery.onClick.AddListener(
            actions.Health.ToggleAutomaticEmergencySurgery);

        RectTransform substanceCommandRow = CreateRect(
            "SubstanceCommandRow",
            healthContent);
        substanceCommandRow.anchorMin = new Vector2(0f, 1f);
        substanceCommandRow.anchorMax = new Vector2(1f, 1f);
        substanceCommandRow.pivot = new Vector2(0.5f, 1f);
        substanceCommandRow.anchoredPosition = new Vector2(0f, -49f);
        substanceCommandRow.sizeDelta = new Vector2(0f, 44f);
        HorizontalLayoutGroup substanceCommands =
            substanceCommandRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        substanceCommands.spacing = 5f;
        substanceCommands.childControlWidth = true;
        substanceCommands.childControlHeight = true;
        substanceCommands.childForceExpandWidth = true;
        substanceCommands.childForceExpandHeight = true;
        Button substanceSelection = CreateButton(
            "SubstanceSelection",
            substanceCommandRow,
            CharacterSummaryUiTextQuery.Get(
                "CharacterSummary.Health.Action.SelectSubstance"));
        Button substancePolicy = CreateButton(
            "SubstancePolicy",
            substanceCommandRow,
            CharacterSummaryUiTextQuery.Get(
                "CharacterSummary.Health.Action.SubstanceProhibited"));
        substanceSelection.onClick.AddListener(actions.Health.SelectNextSubstance);
        substancePolicy.onClick.AddListener(
            actions.Health.CycleSelectedSubstancePolicy);
        TMP_Text healthSummaryText = CreateScrollableText(
            "HealthContentViewport",
            "HealthSummaryText",
            healthContent,
            CharacterSummaryUiTextQuery.Get("CharacterSummary.Health.Empty"),
            minHeight: 360f,
            fillParent: true);
        RectTransform healthViewport =
            healthSummaryText.transform.parent as RectTransform;
        if (healthViewport != null)
        {
            healthViewport.offsetMax = new Vector2(0f, -99f);
        }
        healthContent.gameObject.SetActive(false);

        RectTransform combatContent = CreateRect("CombatContent", content);
        SetStretch(combatContent, Vector2.zero, Vector2.zero);
        RectTransform combatCommands = CreateRect("CombatCommands", combatContent);
        combatCommands.anchorMin = new Vector2(0f, 1f);
        combatCommands.anchorMax = new Vector2(1f, 1f);
        combatCommands.pivot = new Vector2(0.5f, 1f);
        combatCommands.anchoredPosition = Vector2.zero;
        combatCommands.sizeDelta = new Vector2(0f, 44f);
        HorizontalLayoutGroup combatCommandLayout =
            combatCommands.gameObject.AddComponent<HorizontalLayoutGroup>();
        combatCommandLayout.spacing = 5f;
        combatCommandLayout.childAlignment = TextAnchor.MiddleLeft;
        combatCommandLayout.childControlWidth = true;
        combatCommandLayout.childControlHeight = true;
        combatCommandLayout.childForceExpandWidth = true;
        combatCommandLayout.childForceExpandHeight = true;

        Button loadoutButton = CreateButton("LoadoutButton", combatCommands, CharacterSummaryUiTextQuery.Get("CharacterSummary.Combat.Action.Loadout"));
        Button weaponButton = CreateButton("WeaponButton", combatCommands, CharacterSummaryUiTextQuery.Get("CharacterSummary.Combat.Action.SwitchWeapon"));
        Button reloadButton = CreateButton("ReloadButton", combatCommands, CharacterSummaryUiTextQuery.Get("CharacterSummary.Combat.Action.Reload"));
        Button fireModeButton = CreateButton("FireModeButton", combatCommands, CharacterSummaryUiTextQuery.Get("CharacterSummary.Combat.Action.Aimed"));
        Button holdFireButton = CreateButton("HoldFireButton", combatCommands, CharacterSummaryUiTextQuery.Get("CharacterSummary.Combat.Action.FireAllowed"));
        Button repairButton = CreateButton("RepairButton", combatCommands, CharacterSummaryUiTextQuery.Get("CharacterSummary.Combat.Action.Repair"));
        loadoutButton.onClick.AddListener(actions.Combat.ToggleLoadout);
        weaponButton.onClick.AddListener(actions.Combat.CycleWeapon);
        reloadButton.onClick.AddListener(actions.Combat.Reload);
        fireModeButton.onClick.AddListener(actions.Combat.CycleFireMode);
        holdFireButton.onClick.AddListener(actions.Combat.ToggleHoldFire);
        repairButton.onClick.AddListener(actions.Combat.RequestRepair);

        TMP_Text combatSummaryText = CreateScrollableText(
            "CombatContentViewport",
            "CombatSummaryText",
            combatContent,
            CharacterSummaryUiTextQuery.Get("CharacterSummary.Combat.Empty"),
            minHeight: 360f,
            fillParent: true);
        RectTransform combatViewport = combatSummaryText.transform.parent as RectTransform;
        if (combatViewport != null)
        {
            combatViewport.offsetMax = new Vector2(0f, -50f);
        }
        combatContent.gameObject.SetActive(false);

        RectTransform growthContent = CreateRect("GrowthContent", content);
        SetStretch(growthContent, Vector2.zero, Vector2.zero);
        growthContent.gameObject.AddComponent<Image>().color = DungeonUiTheme.Panel;
        growthContent.gameObject.AddComponent<RectMask2D>();
        ScrollRect growthScroll = growthContent.gameObject.AddComponent<ScrollRect>();
        growthScroll.viewport = growthContent;
        growthScroll.horizontal = false;
        growthScroll.vertical = true;
        growthScroll.movementType = ScrollRect.MovementType.Clamped;
        growthScroll.scrollSensitivity = 28f;

        RectTransform growthList = CreateRect("GrowthList", growthContent);
        growthList.anchorMin = new Vector2(0f, 1f);
        growthList.anchorMax = new Vector2(1f, 1f);
        growthList.pivot = new Vector2(0.5f, 1f);
        growthList.anchoredPosition = Vector2.zero;
        growthList.sizeDelta = Vector2.zero;
        VerticalLayoutGroup growthVertical = growthList.gameObject.AddComponent<VerticalLayoutGroup>();
        growthVertical.spacing = 6f;
        growthVertical.childAlignment = TextAnchor.UpperLeft;
        growthVertical.childControlWidth = true;
        growthVertical.childControlHeight = true;
        growthVertical.childForceExpandWidth = true;
        growthVertical.childForceExpandHeight = false;
        ContentSizeFitter growthFitter = growthList.gameObject.AddComponent<ContentSizeFitter>();
        growthFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        growthFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        growthScroll.content = growthList;

        CreateSectionLabel(growthList, "Level", CharacterSummaryUiTextQuery.Get("CharacterSummary.Section.Level"));
        Slider experience = CreateMeterRow(growthList, "Experience", CharacterSummaryUiTextQuery.Get("CharacterSummary.Meter.Experience"), 48f);
        TMP_Text progressionSummary = CreateText(
            "ProgressionSummaryText",
            growthList,
            15f,
            FontStyles.Normal);
        progressionSummary.color = DungeonUiTheme.TextSecondary;
        progressionSummary.alignment = TextAlignmentOptions.TopLeft;
        progressionSummary.textWrappingMode = TextWrappingModes.Normal;
        progressionSummary.overflowMode = TextOverflowModes.Overflow;
        progressionSummary.lineSpacing = 5f;
        progressionSummary.margin = new Vector4(6f, 0f, 6f, 0f);
        LayoutElement progressionLayout = progressionSummary.gameObject.AddComponent<LayoutElement>();
        progressionLayout.minHeight = 178f;
        progressionLayout.preferredHeight = 214f;
        CreateSectionLabel(growthList, "SkillSlots", CharacterSummaryUiTextQuery.Get("CharacterSummary.Section.SkillSlots"));

        Button[] skillButtons = new Button[10];
        for (int i = 0; i < skillButtons.Length; i++)
        {
            int capturedIndex = i;
            Button skillButton = CreateButton($"Skill_{i}", growthList, string.Empty);
            LayoutElement skillLayout = skillButton.gameObject.AddComponent<LayoutElement>();
            skillLayout.minHeight = 42f;
            skillLayout.preferredHeight = 42f;
            TMP_Text skillLabel = skillButton.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (skillLabel != null)
            {
                skillLabel.fontSize = 14f;
                skillLabel.alignment = TextAlignmentOptions.MidlineLeft;
                skillLabel.textWrappingMode = TextWrappingModes.Normal;
            }

            skillButton.onClick.AddListener(
                () => actions.Growth.ToggleSkillAt(capturedIndex));
            skillButtons[i] = skillButton;
        }

        growthContent.gameObject.SetActive(false);

        RectTransform moodContent = CreateRect("MoodContent", content);
        SetStretch(moodContent, Vector2.zero, Vector2.zero);
        VerticalLayoutGroup moodVertical = moodContent.gameObject.AddComponent<VerticalLayoutGroup>();
        moodVertical.spacing = 6f;
        moodVertical.childAlignment = TextAnchor.UpperLeft;
        moodVertical.childControlWidth = true;
        moodVertical.childControlHeight = true;
        moodVertical.childForceExpandWidth = true;
        moodVertical.childForceExpandHeight = false;

        CreateSectionLabel(moodContent, "Mood", CharacterSummaryUiTextQuery.Get("CharacterSummary.Section.Mood"));
        Slider mood = CreateMeterRow(moodContent, "MoodOverview", CharacterSummaryUiTextQuery.Get("CharacterSummary.Meter.CurrentMood"), 48f);
        TMP_Text moodSummaryText = CreateText("MoodSummaryText", moodContent, 15f, FontStyles.Normal);
        moodSummaryText.text = CharacterSummaryUiTextQuery.Get(
            "CharacterSummary.Mood.DefaultSummary");
        moodSummaryText.color = DungeonUiTheme.TextSecondary;
        moodSummaryText.alignment = TextAlignmentOptions.MidlineLeft;
        moodSummaryText.margin = new Vector4(6f, 0f, 6f, 0f);
        moodSummaryText.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        CreateSectionLabel(moodContent, "MoodFactors", CharacterSummaryUiTextQuery.Get("CharacterSummary.Section.MoodFactors"));

        TMP_Text moodFactorsText = CreateScrollableText(
            "MoodFactorsViewport",
            "MoodFactorsText",
            moodContent,
            CharacterSummaryUiTextQuery.Get("CharacterSummary.Mood.NoFactors"),
            minHeight: 240f);
        moodContent.gameObject.SetActive(false);

        RectTransform recordsContent = CreateRect("RecordsContent", content);
        SetStretch(recordsContent, Vector2.zero, Vector2.zero);
        TMP_Text logText = CreateScrollableText(
            "RecordsContentViewport",
            "CharacterLogText",
            recordsContent,
            CharacterSummaryUiTextQuery.Get("CharacterSummary.Log.Empty"),
            minHeight: 360f,
            fillParent: true);
        recordsContent.gameObject.SetActive(false);

        RectTransform aiContent = CreateRect("AiContent", content);
        SetStretch(aiContent, Vector2.zero, Vector2.zero);
        TMP_Text aiSummaryText = CreateScrollableText(
            "AiContentViewport",
            "AiSummaryText",
            aiContent,
            CharacterSummaryUiTextQuery.Get("CharacterSummary.AI.Empty"),
            minHeight: 360f,
            fillParent: true);
        aiContent.gameObject.SetActive(false);

        RectTransform detailedOverlay = CreateRect("DetailedOverlay", view);
        SetStretch(detailedOverlay, Vector2.zero, Vector2.zero);
        detailedOverlay.gameObject.AddComponent<Image>().color = DungeonUiTheme.Panel;

        RectTransform detailedHeader = CreateRect("DetailedHeader", detailedOverlay);
        detailedHeader.anchorMin = new Vector2(0f, 1f);
        detailedHeader.anchorMax = new Vector2(1f, 1f);
        detailedHeader.pivot = new Vector2(0.5f, 1f);
        detailedHeader.sizeDelta = new Vector2(0f, 58f);
        detailedHeader.gameObject.AddComponent<Image>().color = DungeonUiTheme.SurfaceRaised;
        TMP_Text detailedTitle = CreateText("DetailedTitle", detailedHeader, 22f, FontStyles.Bold);
        detailedTitle.text = CharacterSummaryUiTextQuery.Get(
            "CharacterSummary.Detailed.Title");
        detailedTitle.alignment = TextAlignmentOptions.MidlineLeft;
        SetStretch(detailedTitle.rectTransform, new Vector2(16f, 8f), new Vector2(-94f, -8f));
        Button detailedClose = CreateButton(
            "DetailedClose",
            detailedHeader,
            CharacterSummaryUiTextQuery.Get("CharacterSummary.Action.Close"));
        RectTransform detailedCloseRect = detailedClose.GetComponent<RectTransform>();
        detailedCloseRect.anchorMin = new Vector2(1f, 0.5f);
        detailedCloseRect.anchorMax = new Vector2(1f, 0.5f);
        detailedCloseRect.pivot = new Vector2(1f, 0.5f);
        detailedCloseRect.anchoredPosition = new Vector2(-12f, 0f);
        detailedCloseRect.sizeDelta = new Vector2(68f, 36f);
        detailedClose.onClick.AddListener(actions.Popup.CloseDetailedStats);

        RectTransform detailedTabs = CreateRect("DetailedTabs", detailedOverlay);
        detailedTabs.anchorMin = new Vector2(0f, 1f);
        detailedTabs.anchorMax = new Vector2(1f, 1f);
        detailedTabs.pivot = new Vector2(0.5f, 1f);
        detailedTabs.anchoredPosition = new Vector2(0f, -64f);
        detailedTabs.sizeDelta = new Vector2(0f, 80f);
        GridLayoutGroup detailedTabGrid = detailedTabs.gameObject.AddComponent<GridLayoutGroup>();
        detailedTabGrid.padding = new RectOffset(14, 14, 0, 0);
        detailedTabGrid.spacing = new Vector2(6f, 6f);
        detailedTabGrid.cellSize = new Vector2(153f, 36f);
        detailedTabGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        detailedTabGrid.constraintCount = 3;
        CharacterDetailedStatsTab[] detailedTabValues =
            (CharacterDetailedStatsTab[])Enum.GetValues(typeof(CharacterDetailedStatsTab));
        Button[] detailedTabButtons = new Button[detailedTabValues.Length];
        for (int i = 0; i < detailedTabValues.Length; i++)
        {
            CharacterDetailedStatsTab tab = detailedTabValues[i];
            Button button = CreateButton(
                "DetailedTab_" + tab,
                detailedTabs,
                CharacterDetailedStatsRuntime.TabLabel(tab));
            button.onClick.AddListener(
                () => actions.Popup.ShowDetailedStatsTab(tab));
            detailedTabButtons[i] = button;
        }

        TMP_Text detailedStatsText = CreateScrollableText(
            "DetailedViewport",
            "DetailedStatsText",
            detailedOverlay,
            CharacterSummaryUiTextQuery.Get("CharacterSummary.Detailed.Empty"),
            minHeight: 400f,
            fillParent: true);
        RectTransform detailedViewport = detailedStatsText.transform.parent as RectTransform;
        if (detailedViewport != null)
        {
            detailedViewport.offsetMin = new Vector2(14f, 14f);
            detailedViewport.offsetMax = new Vector2(-14f, -150f);
        }
        detailedOverlay.gameObject.SetActive(false);

        viewBinding.BindGeneratedView(
            nameText,
            profileText,
            health,
            mood,
            fun,
            hunger,
            sleep,
            excretion,
            hygiene,
            moodSummaryText,
            moodFactorsText,
            aiSummaryText,
            carrySummary,
            logText);
        viewBinding.BindGeneratedGrowth(experience, progressionSummary, skillButtons);
        viewBinding.BindGeneratedSurvival(
            thirst,
            healthSummaryText,
            healthContent.gameObject,
            healthTabButton,
            captivityCommand,
            dietPolicy,
            surgeryCommand,
            automaticSurgery,
            substanceSelection,
            substancePolicy);
        viewBinding.BindGeneratedCombat(
            combatSummaryText,
            combatContent.gameObject,
            combatTabButton,
            loadoutButton,
            weaponButton,
            reloadButton,
            fireModeButton,
            holdFireButton,
            repairButton);
        viewBinding.BindGeneratedTabs(
            statusContent.gameObject,
            growthContent.gameObject,
            moodContent.gameObject,
            recordsContent.gameObject,
            aiContent.gameObject,
            statusTabButton,
            growthTabButton,
            moodTabButton,
            recordsTabButton,
            aiTabButton);
        viewBinding.BindGeneratedDetailedStats(
            detailedStatsButton,
            detailedOverlay.gameObject,
            detailedTitle,
            detailedStatsText,
            detailedTabButtons);
        return view;
    }

    private void Bind(
        ICharacterSummaryGeneratedView viewBinding,
        Transform generated)
    {
        if (viewBinding == null || generated == null)
        {
            return;
        }

        viewBinding.BindGeneratedView(
            generated.Find("Header/CharacterName")?.GetComponent<TMP_Text>(),
            generated.Find("Header/CharacterProfile")?.GetComponent<TMP_Text>(),
            FindSlider(generated, "Health"),
            FindSlider(generated, "MoodOverview", "MoodContent"),
            FindSlider(generated, "Fun"),
            FindSlider(generated, "Hunger"),
            FindSlider(generated, "Sleep"),
            FindSlider(generated, "Excretion"),
            FindSlider(generated, "Hygiene"),
            generated.Find("Content/MoodContent/MoodSummaryText")?.GetComponent<TMP_Text>(),
            generated.Find("Content/MoodContent/MoodFactorsViewport/MoodFactorsText")?.GetComponent<TMP_Text>(),
            generated.Find("Content/AiContent/AiContentViewport/AiSummaryText")?.GetComponent<TMP_Text>(),
            generated.Find("Content/StatusContent/CarrySummaryText")?.GetComponent<TMP_Text>(),
            generated.Find("Content/RecordsContent/RecordsContentViewport/CharacterLogText")?.GetComponent<TMP_Text>());

        Button[] skillButtons = new Button[10];
        for (int i = 0; i < skillButtons.Length; i++)
        {
            skillButtons[i] = generated.Find($"Content/GrowthContent/GrowthList/Skill_{i}")?.GetComponent<Button>();
        }

        viewBinding.BindGeneratedGrowth(
            FindSlider(generated, "Experience", "GrowthContent/GrowthList"),
            generated.Find("Content/GrowthContent/GrowthList/ProgressionSummaryText")?.GetComponent<TMP_Text>(),
            skillButtons);
        viewBinding.BindGeneratedSurvival(
            FindSlider(generated, "Thirst"),
            generated.Find("Content/HealthContent/HealthContentViewport/HealthSummaryText")?.GetComponent<TMP_Text>(),
            generated.Find("Content/HealthContent")?.gameObject,
            generated.Find("TabBar/HealthTab")?.GetComponent<Button>(),
            generated.Find("Content/HealthContent/HealthCommandRow/CaptivityCommand")?.GetComponent<Button>(),
            generated.Find("Content/HealthContent/HealthCommandRow/DietPolicy")?.GetComponent<Button>(),
            generated.Find("Content/HealthContent/HealthCommandRow/SurgeryCommand")?.GetComponent<Button>(),
            generated.Find("Content/HealthContent/HealthCommandRow/AutomaticSurgery")?.GetComponent<Button>(),
            generated.Find("Content/HealthContent/SubstanceCommandRow/SubstanceSelection")?.GetComponent<Button>(),
            generated.Find("Content/HealthContent/SubstanceCommandRow/SubstancePolicy")?.GetComponent<Button>());
        viewBinding.BindGeneratedCombat(
            generated.Find("Content/CombatContent/CombatContentViewport/CombatSummaryText")?.GetComponent<TMP_Text>(),
            generated.Find("Content/CombatContent")?.gameObject,
            generated.Find("TabBar/CombatTab")?.GetComponent<Button>(),
            generated.Find("Content/CombatContent/CombatCommands/LoadoutButton")?.GetComponent<Button>(),
            generated.Find("Content/CombatContent/CombatCommands/WeaponButton")?.GetComponent<Button>(),
            generated.Find("Content/CombatContent/CombatCommands/ReloadButton")?.GetComponent<Button>(),
            generated.Find("Content/CombatContent/CombatCommands/FireModeButton")?.GetComponent<Button>(),
            generated.Find("Content/CombatContent/CombatCommands/HoldFireButton")?.GetComponent<Button>(),
            generated.Find("Content/CombatContent/CombatCommands/RepairButton")?.GetComponent<Button>());
        viewBinding.BindGeneratedTabs(
            generated.Find("Content/StatusContent")?.gameObject,
            generated.Find("Content/GrowthContent")?.gameObject,
            generated.Find("Content/MoodContent")?.gameObject,
            generated.Find("Content/RecordsContent")?.gameObject,
            generated.Find("Content/AiContent")?.gameObject,
            generated.Find("TabBar/StatusTab")?.GetComponent<Button>(),
            generated.Find("TabBar/GrowthTab")?.GetComponent<Button>(),
            generated.Find("TabBar/MoodTab")?.GetComponent<Button>(),
            generated.Find("TabBar/RecordsTab")?.GetComponent<Button>(),
            generated.Find("TabBar/AiTab")?.GetComponent<Button>());
        Button[] detailedTabButtons = Enum.GetValues(typeof(CharacterDetailedStatsTab))
            .Cast<CharacterDetailedStatsTab>()
            .Select(tab => generated.Find($"DetailedOverlay/DetailedTabs/DetailedTab_{tab}")
                ?.GetComponent<Button>())
            .ToArray();
        viewBinding.BindGeneratedDetailedStats(
            generated.Find("Header/DetailedStatsButton")?.GetComponent<Button>(),
            generated.Find("DetailedOverlay")?.gameObject,
            generated.Find("DetailedOverlay/DetailedHeader/DetailedTitle")?.GetComponent<TMP_Text>(),
            generated.Find("DetailedOverlay/DetailedViewport/DetailedStatsText")?.GetComponent<TMP_Text>(),
            detailedTabButtons);
    }

    private static Slider FindSlider(Transform root, string rowName, string contentName = "StatusContent")
    {
        return root.Find($"Content/{contentName}/{rowName}/Track")?.GetComponent<Slider>();
    }

    private Button CreateTabButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        Button button = CreateButton(name, parent, label);
        button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        button.onClick.AddListener(onClick);
        return button;
    }

    private TMP_Text CreateScrollableText(
        string viewportName,
        string textName,
        Transform parent,
        string defaultText,
        float minHeight,
        bool fillParent = false)
    {
        RectTransform viewport = CreateRect(viewportName, parent);
        Image image = viewport.gameObject.AddComponent<Image>();
        image.color = DungeonUiTheme.SurfaceMuted;
        viewport.gameObject.AddComponent<RectMask2D>();
        if (fillParent)
        {
            SetStretch(viewport, Vector2.zero, Vector2.zero);
        }
        else
        {
            LayoutElement viewportLayout = viewport.gameObject.AddComponent<LayoutElement>();
            viewportLayout.minHeight = minHeight;
            viewportLayout.flexibleHeight = 1f;
        }

        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        TMP_Text text = CreateText(textName, viewport, 16f, FontStyles.Normal);
        text.text = defaultText;
        text.color = DungeonUiTheme.TextSecondary;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.lineSpacing = 8f;
        text.margin = new Vector4(14f, 12f, 14f, 12f);
        text.rectTransform.anchorMin = new Vector2(0f, 1f);
        text.rectTransform.anchorMax = new Vector2(1f, 1f);
        text.rectTransform.pivot = new Vector2(0.5f, 1f);
        text.rectTransform.anchoredPosition = Vector2.zero;
        text.rectTransform.sizeDelta = Vector2.zero;
        ContentSizeFitter fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = text.rectTransform;
        return text;
    }

    private TMP_Text CreateSectionLabel(
        Transform parent,
        string stableName,
        string text)
    {
        TMP_Text label = CreateText(
            "Section_" + stableName,
            parent,
            16f,
            FontStyles.Bold);
        label.text = text;
        label.color = DungeonUiTheme.TextPrimary;
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.margin = new Vector4(2f, 0f, 0f, 0f);
        LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 24f;
        layout.preferredHeight = 24f;
        return label;
    }

    private Slider CreateMeterRow(Transform parent, string name, string labelText, float height)
    {
        RectTransform row = CreateRect(name, parent);
        row.gameObject.AddComponent<Image>().color = DungeonUiTheme.Surface;
        LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = height;
        rowLayout.preferredHeight = height;

        HorizontalLayoutGroup horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.padding = new RectOffset(10, 10, 6, 6);
        horizontal.spacing = 10f;
        horizontal.childAlignment = TextAnchor.MiddleLeft;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = false;

        TMP_Text label = CreateText("Label", row, 16f, FontStyles.Bold);
        label.text = labelText;
        label.color = DungeonUiTheme.TextPrimary;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.minWidth = 74f;
        labelLayout.preferredWidth = 74f;

        RectTransform track = CreateRect("Track", row);
        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = DungeonUiTheme.SurfaceMuted;
        LayoutElement trackLayout = track.gameObject.AddComponent<LayoutElement>();
        trackLayout.minWidth = 120f;
        trackLayout.flexibleWidth = 1f;
        trackLayout.preferredHeight = 16f;

        RectTransform fillArea = CreateRect("FillArea", track);
        SetStretch(fillArea, new Vector2(3f, 3f), new Vector2(-3f, -3f));
        RectTransform fill = CreateRect("Fill", fillArea);
        SetStretch(fill, Vector2.zero, Vector2.zero);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = DungeonUiTheme.Good;

        Slider slider = track.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fill;
        slider.targetGraphic = fillImage;
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;

        TMP_Text value = CreateText("Value", row, 15f, FontStyles.Bold);
        value.text = "100";
        value.color = DungeonUiTheme.TextSecondary;
        value.alignment = TextAlignmentOptions.MidlineRight;
        LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>();
        float valueWidth = name == "Health"
            ? 142f
            : name == "Experience"
                ? 92f
                : 48f;
        valueLayout.minWidth = valueWidth;
        valueLayout.preferredWidth = valueWidth;
        return slider;
    }

    private Button CreateButton(string name, Transform parent, string labelText)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text label = CreateText("Label", rect, 15f, FontStyles.Bold);
        label.text = labelText;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        SetStretch(label.rectTransform, new Vector2(6f, 2f), new Vector2(-6f, -2f));
        DungeonUiTheme.StyleButton(button);
        return button;
    }

    private TMP_Text CreateText(string name, Transform parent, float fontSize, FontStyles style)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmpKoreanFontService.Apply(text);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = DungeonUiTheme.TextPrimary;
        text.characterSpacing = 0f;
        text.raycastTarget = false;
        return text;
    }
}
