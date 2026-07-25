using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public interface ICharacterSummaryRuntimeLogFactory
{
    void Ensure(CharacterSummeryInfo owner, GameObject uiRoot);
    void ApplyFonts(Transform root);
}

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

    public void Ensure(CharacterSummeryInfo owner, GameObject uiRoot)
    {
        if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner));
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
                || generated.Find("Content/CombatContent/CombatContentViewport/CombatSummaryText") == null
                || generated.Find("Content/CombatContent/CombatCommands/LoadoutButton") == null
                || generated.Find("Content/StatusContent/CarrySummaryText") == null
                || generated.Find("Content/AiContent/AiContentViewport/AiSummaryText") == null))
        {
            UnityEngine.Object.DestroyImmediate(generated.gameObject);
            generated = null;
        }

        if (generated == null)
        {
            DisableLegacyChildren(uiRoot.transform);
            generated = CreateView(owner, uiRoot.transform);
        }

        Bind(owner, generated);
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

    private Transform CreateView(CharacterSummeryInfo owner, Transform parent)
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
        SetStretch(nameText.rectTransform, new Vector2(18f, 32f), new Vector2(-92f, -8f));

        TMP_Text profileText = CreateText("CharacterProfile", header, 15f, FontStyles.Normal);
        profileText.alignment = TextAlignmentOptions.TopLeft;
        profileText.color = DungeonUiTheme.TextSecondary;
        profileText.textWrappingMode = TextWrappingModes.NoWrap;
        profileText.overflowMode = TextOverflowModes.Truncate;
        SetStretch(profileText.rectTransform, new Vector2(18f, 8f), new Vector2(-92f, -44f));

        Button closeButton = CreateButton("CloseButton", header, "닫기");
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-12f, -12f);
        closeRect.sizeDelta = new Vector2(68f, 36f);
        closeButton.onClick.AddListener(owner.OnClose);

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

        Button statusTabButton = CreateTabButton("StatusTab", tabBar, "상태", owner.ShowStatusTab);
        Button healthTabButton = CreateTabButton("HealthTab", tabBar, "건강", owner.ShowHealthTab);
        Button combatTabButton = CreateTabButton("CombatTab", tabBar, "전투", owner.ShowCombatTab);
        Button growthTabButton = CreateTabButton("GrowthTab", tabBar, "성장", owner.ShowGrowthTab);
        Button moodTabButton = CreateTabButton("MoodTab", tabBar, "기분", owner.ShowMoodTab);
        Button recordsTabButton = CreateTabButton("RecordsTab", tabBar, "기록", owner.ShowRecordsTab);
        Button aiTabButton = CreateTabButton("AiTab", tabBar, "AI", owner.ShowAiTab);

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

        CreateSectionLabel(statusContent, "상태");
        Slider health = CreateMeterRow(statusContent, "Health", "체력", 46f);
        CreateSectionLabel(statusContent, "욕구");
        Slider hunger = CreateMeterRow(statusContent, "Hunger", "포만감", 40f);
        Slider thirst = CreateMeterRow(statusContent, "Thirst", "갈증", 40f);
        Slider fun = CreateMeterRow(statusContent, "Fun", "재미", 40f);
        Slider sleep = CreateMeterRow(statusContent, "Sleep", "휴식", 40f);
        Slider excretion = CreateMeterRow(statusContent, "Excretion", "배변", 40f);
        Slider hygiene = CreateMeterRow(statusContent, "Hygiene", "위생", 40f);

        TMP_Text carrySummary = CreateText("CarrySummaryText", statusContent, 14f, FontStyles.Normal);
        carrySummary.text = "소지 아이템 없음";
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
        TMP_Text healthSummaryText = CreateScrollableText(
            "HealthContentViewport",
            "HealthSummaryText",
            healthContent,
            "결핍 건강 정보가 없습니다.",
            minHeight: 360f,
            fillParent: true);
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

        Button loadoutButton = CreateButton("LoadoutButton", combatCommands, "전투 장비");
        Button weaponButton = CreateButton("WeaponButton", combatCommands, "무기 교체");
        Button reloadButton = CreateButton("ReloadButton", combatCommands, "재장전");
        Button fireModeButton = CreateButton("FireModeButton", combatCommands, "조준");
        Button holdFireButton = CreateButton("HoldFireButton", combatCommands, "사격 허용");
        Button repairButton = CreateButton("RepairButton", combatCommands, "수리 요청");
        loadoutButton.onClick.AddListener(owner.ToggleCombatLoadout);
        weaponButton.onClick.AddListener(owner.CycleCombatWeapon);
        reloadButton.onClick.AddListener(owner.ReloadCombatWeapon);
        fireModeButton.onClick.AddListener(owner.CycleCombatFireMode);
        holdFireButton.onClick.AddListener(owner.ToggleCombatHoldFire);
        repairButton.onClick.AddListener(owner.RequestCombatEquipmentRepair);

        TMP_Text combatSummaryText = CreateScrollableText(
            "CombatContentViewport",
            "CombatSummaryText",
            combatContent,
            "전투 정보가 없습니다.",
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

        CreateSectionLabel(growthList, "레벨");
        Slider experience = CreateMeterRow(growthList, "Experience", "경험치", 48f);
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
        CreateSectionLabel(growthList, "기술 슬롯과 후보");

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

            skillButton.onClick.AddListener(() => owner.ToggleSkillAt(capturedIndex));
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

        CreateSectionLabel(moodContent, "기분");
        Slider mood = CreateMeterRow(moodContent, "MoodOverview", "현재 기분", 48f);
        TMP_Text moodSummaryText = CreateText("MoodSummaryText", moodContent, 15f, FontStyles.Normal);
        moodSummaryText.text = "평온함 · 기준 50 · 보정 +0";
        moodSummaryText.color = DungeonUiTheme.TextSecondary;
        moodSummaryText.alignment = TextAlignmentOptions.MidlineLeft;
        moodSummaryText.margin = new Vector4(6f, 0f, 6f, 0f);
        moodSummaryText.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        CreateSectionLabel(moodContent, "기분 요인");

        TMP_Text moodFactorsText = CreateScrollableText(
            "MoodFactorsViewport",
            "MoodFactorsText",
            moodContent,
            "현재 기분을 바꾸는 요인이 없습니다.",
            minHeight: 240f);
        moodContent.gameObject.SetActive(false);

        RectTransform recordsContent = CreateRect("RecordsContent", content);
        SetStretch(recordsContent, Vector2.zero, Vector2.zero);
        TMP_Text logText = CreateScrollableText(
            "RecordsContentViewport",
            "CharacterLogText",
            recordsContent,
            "아직 기록이 없습니다.",
            minHeight: 360f,
            fillParent: true);
        recordsContent.gameObject.SetActive(false);

        RectTransform aiContent = CreateRect("AiContent", content);
        SetStretch(aiContent, Vector2.zero, Vector2.zero);
        TMP_Text aiSummaryText = CreateScrollableText(
            "AiContentViewport",
            "AiSummaryText",
            aiContent,
            "AI 판단 기록이 아직 없습니다.",
            minHeight: 360f,
            fillParent: true);
        aiContent.gameObject.SetActive(false);

        owner.BindGeneratedView(
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
        owner.BindGeneratedGrowth(experience, progressionSummary, skillButtons);
        owner.BindGeneratedSurvival(
            thirst,
            healthSummaryText,
            healthContent.gameObject,
            healthTabButton);
        owner.BindGeneratedCombat(
            combatSummaryText,
            combatContent.gameObject,
            combatTabButton,
            loadoutButton,
            weaponButton,
            reloadButton,
            fireModeButton,
            holdFireButton,
            repairButton);
        owner.BindGeneratedTabs(
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
        return view;
    }

    private void Bind(CharacterSummeryInfo owner, Transform generated)
    {
        if (owner == null || generated == null)
        {
            return;
        }

        owner.BindGeneratedView(
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

        owner.BindGeneratedGrowth(
            FindSlider(generated, "Experience", "GrowthContent/GrowthList"),
            generated.Find("Content/GrowthContent/GrowthList/ProgressionSummaryText")?.GetComponent<TMP_Text>(),
            skillButtons);
        owner.BindGeneratedSurvival(
            FindSlider(generated, "Thirst"),
            generated.Find("Content/HealthContent/HealthContentViewport/HealthSummaryText")?.GetComponent<TMP_Text>(),
            generated.Find("Content/HealthContent")?.gameObject,
            generated.Find("TabBar/HealthTab")?.GetComponent<Button>());
        owner.BindGeneratedCombat(
            generated.Find("Content/CombatContent/CombatContentViewport/CombatSummaryText")?.GetComponent<TMP_Text>(),
            generated.Find("Content/CombatContent")?.gameObject,
            generated.Find("TabBar/CombatTab")?.GetComponent<Button>(),
            generated.Find("Content/CombatContent/CombatCommands/LoadoutButton")?.GetComponent<Button>(),
            generated.Find("Content/CombatContent/CombatCommands/WeaponButton")?.GetComponent<Button>(),
            generated.Find("Content/CombatContent/CombatCommands/ReloadButton")?.GetComponent<Button>(),
            generated.Find("Content/CombatContent/CombatCommands/FireModeButton")?.GetComponent<Button>(),
            generated.Find("Content/CombatContent/CombatCommands/HoldFireButton")?.GetComponent<Button>(),
            generated.Find("Content/CombatContent/CombatCommands/RepairButton")?.GetComponent<Button>());
        owner.BindGeneratedTabs(
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

    private TMP_Text CreateSectionLabel(Transform parent, string text)
    {
        TMP_Text label = CreateText("Section_" + text, parent, 16f, FontStyles.Bold);
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

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static void ConfigurePanelBounds(GameObject uiRoot)
    {
        RectTransform wrapper = uiRoot.transform.parent as RectTransform;
        if (wrapper != null)
        {
            wrapper.anchorMin = Vector2.zero;
            wrapper.anchorMax = Vector2.zero;
            wrapper.pivot = Vector2.zero;
            wrapper.anchoredPosition = new Vector2(24f, 80f);
            wrapper.sizeDelta = new Vector2(500f, 700f);
        }

        RectTransform rootRect = uiRoot.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            SetStretch(rootRect, Vector2.zero, Vector2.zero);
        }

        Image background = uiRoot.GetComponent<Image>();
        if (background == null)
        {
            background = uiRoot.AddComponent<Image>();
        }

        background.color = DungeonUiTheme.Panel;
    }

    private static void DisableLegacyChildren(Transform root)
    {
        foreach (Transform child in root)
        {
            child.gameObject.SetActive(false);
        }
    }

    private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
