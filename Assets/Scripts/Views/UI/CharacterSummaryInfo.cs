using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class CharacterSummaryInfo : UIPopUp, ICharacterSummaryGeneratedView
{
    public GameObject UI;
    public TMP_Text ObjectName;
    public TMP_Text logText;
    public Slider mood;
    public Slider fun;
    public Slider hunger;
    public Slider thirst;
    public Slider sleep;
    public Slider excretion;
    public Slider hygiene;

    private CharacterActor actor;
    private CharacterStats characterStats;
    private CharacterLog characterLog;
    private CharacterProgression progression;
    private TMP_Text moodSummaryText;
    private TMP_Text moodFactorsText;
    private GameObject statusTabContent;
    private GameObject healthTabContent;
    private GameObject growthTabContent;
    private GameObject moodTabContent;
    private GameObject recordsTabContent;
    private GameObject aiTabContent;
    private GameObject combatTabContent;
    private Button statusTabButton;
    private Button healthTabButton;
    private Button growthTabButton;
    private Button moodTabButton;
    private Button recordsTabButton;
    private Button aiTabButton;
    private Button combatTabButton;
    private float nextVitalsRefreshAt;
    private CharacterSummaryShellPresenter shellPresenter;
    private CharacterSummaryCombatPresenter combatPresenter;
    private CharacterSummaryHealthPresenter healthPresenter;
    private CharacterSummaryStatusPresenter statusPresenter;
    private CharacterSummaryAiPresenter aiPresenter;
    private CharacterSummaryGrowthPresenter growthPresenter;
    private IUiClock uiClock;
    private IGameEventBus gameEventBus;
    private CharacterSummaryViewActions viewActions;
    private IDisposable growthTabRequestedSubscription;
    private IDisposable infoFeedSubscription;

    [Inject]
    public void Construct(
        CharacterSummaryShellPresenter shellPresenter,
        CharacterSummaryCombatPresenter combatPresenter,
        CharacterSummaryHealthPresenter healthPresenter,
        CharacterSummaryStatusPresenter statusPresenter,
        CharacterSummaryAiPresenter aiPresenter,
        CharacterSummaryGrowthPresenter growthPresenter,
        IUiClock uiClock,
        IGameEventBus gameEventBus)
    {
        this.shellPresenter = shellPresenter
            ?? throw new ArgumentNullException(nameof(shellPresenter));
        this.combatPresenter = combatPresenter
            ?? throw new ArgumentNullException(nameof(combatPresenter));
        this.healthPresenter = healthPresenter
            ?? throw new ArgumentNullException(nameof(healthPresenter));
        this.statusPresenter = statusPresenter
            ?? throw new ArgumentNullException(nameof(statusPresenter));
        this.aiPresenter = aiPresenter
            ?? throw new ArgumentNullException(nameof(aiPresenter));
        this.growthPresenter = growthPresenter
            ?? throw new ArgumentNullException(nameof(growthPresenter));
        this.uiClock = uiClock
            ?? throw new ArgumentNullException(nameof(uiClock));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToScopedEvents();
    }

    private void Start()
    {
        shellPresenter.Initialize(this, RequireViewActions(), RequireUiRoot());
    }

    private void Update()
    {
        if (actor == null
            || UI == null
            || !UI.activeInHierarchy
            || uiClock.Time < nextVitalsRefreshAt)
        {
            return;
        }

        nextVitalsRefreshAt = uiClock.Time + 0.25f;
        RefreshProfileAndVitals();
        RefreshCarrySummary();
        RefreshProgression();
        RefreshMoodDetails();
        RefreshHealthDetails();
        RefreshCombatDetails();
        RefreshAiDetails();
        RefreshDetailedStats();
    }

    public void OnTriggerEvent(InfoFeedEvent eventType)
    {
        if (eventType.Target is not CharacterActor nextActor || nextActor == null)
        {
            return;
        }

        UnbindCharacter();

        shellPresenter.Open(
            this,
            this,
            RequireViewActions(),
            RequireUiRoot());

        actor = nextActor;
        GameObject targetObject = actor.gameObject;
        characterStats = targetObject.GetComponent<CharacterStats>();
        characterLog = targetObject.GetComponent<CharacterLog>();
        progression = actor.Progression;
        ObjectName.text = GetDisplayName(targetObject);

        RefreshProfileAndVitals();
        RefreshCarrySummary();
        RefreshProgression();
        RefreshMoodDetails();
        RefreshHealthDetails();
        RefreshCombatDetails();
        RefreshAiDetails();

        if (characterStats != null)
        {
            RefreshStatSliders();
            characterStats.OnStatsInvalidated += RefreshStatSliders;
        }

        if (characterLog != null)
        {
            characterLog.OnLogAdded += OnLogAdded;
            characterLog.OnLogDisplayChanged += RefreshLogText;
        }

        if (progression != null)
        {
            progression.Changed += RefreshProgression;
        }

        RefreshLogText();
    }

    public override void OnClose()
    {
        shellPresenter.CloseDetailedStats();
        RequireUiRoot().SetActive(false);
        UnbindCharacter();
    }

    public void RequestClose()
    {
        shellPresenter.RequestClose(this);
    }

    public void OnStatChange(IReadOnlyDictionary<CharacterCondition, float> stats)
    {
        SetSlider(fun, stats, CharacterCondition.FUN);
        SetSlider(hunger, stats, CharacterCondition.HUNGER);
        SetSlider(thirst, stats, CharacterCondition.THIRST);
        SetSlider(sleep, stats, CharacterCondition.SLEEP);
        SetSlider(excretion, stats, CharacterCondition.EXCRETION);
        SetSlider(hygiene, stats, CharacterCondition.HYGIENE);
        RefreshMoodDetails();
    }

    private void RefreshStatSliders()
    {
        if (characterStats == null)
        {
            return;
        }

        IDictionary<CharacterCondition, float> stats = characterStats.Stats;
        SetSlider(fun, stats, CharacterCondition.FUN);
        SetSlider(hunger, stats, CharacterCondition.HUNGER);
        SetSlider(thirst, stats, CharacterCondition.THIRST);
        SetSlider(sleep, stats, CharacterCondition.SLEEP);
        SetSlider(excretion, stats, CharacterCondition.EXCRETION);
        SetSlider(hygiene, stats, CharacterCondition.HYGIENE);
        RefreshMoodDetails();
    }

    public void BindGeneratedView(
        TMP_Text nameText,
        TMP_Text generatedProfileText,
        Slider healthSlider,
        Slider moodSlider,
        Slider funSlider,
        Slider hungerSlider,
        Slider sleepSlider,
        Slider excretionSlider,
        Slider hygieneSlider,
        TMP_Text generatedMoodSummaryText,
        TMP_Text generatedMoodFactorsText,
        TMP_Text generatedAiSummaryText,
        TMP_Text generatedCarrySummaryText,
        TMP_Text generatedLogText)
    {
        ObjectName = nameText;
        statusPresenter.Bind(generatedProfileText, healthSlider, generatedCarrySummaryText);
        mood = moodSlider;
        fun = funSlider;
        hunger = hungerSlider;
        sleep = sleepSlider;
        excretion = excretionSlider;
        hygiene = hygieneSlider;
        moodSummaryText = generatedMoodSummaryText;
        moodFactorsText = generatedMoodFactorsText;
        aiPresenter.Bind(generatedAiSummaryText);
        logText = generatedLogText;
    }

    public void BindGeneratedTabs(
        GameObject generatedStatusTabContent,
        GameObject generatedGrowthTabContent,
        GameObject generatedMoodTabContent,
        GameObject generatedRecordsTabContent,
        GameObject generatedAiTabContent,
        Button generatedStatusTabButton,
        Button generatedGrowthTabButton,
        Button generatedMoodTabButton,
        Button generatedRecordsTabButton,
        Button generatedAiTabButton)
    {
        statusTabContent = generatedStatusTabContent;
        growthTabContent = generatedGrowthTabContent;
        moodTabContent = generatedMoodTabContent;
        recordsTabContent = generatedRecordsTabContent;
        aiTabContent = generatedAiTabContent;
        statusTabButton = generatedStatusTabButton;
        growthTabButton = generatedGrowthTabButton;
        moodTabButton = generatedMoodTabButton;
        recordsTabButton = generatedRecordsTabButton;
        aiTabButton = generatedAiTabButton;
        ShowStatusTab();
    }

    public void BindGeneratedSurvival(
        Slider generatedThirst,
        TMP_Text generatedHealthSummaryText,
        GameObject generatedHealthTabContent,
        Button generatedHealthTabButton,
        Button generatedCaptivityActionButton,
        Button generatedDietPolicyButton,
        Button generatedSurgeryCommandButton,
        Button generatedAutomaticSurgeryButton,
        Button generatedSubstanceSelectionButton,
        Button generatedSubstancePolicyButton)
    {
        thirst = generatedThirst;
        healthTabContent = generatedHealthTabContent;
        healthTabButton = generatedHealthTabButton;
        healthPresenter.Bind(
            generatedHealthSummaryText,
            generatedCaptivityActionButton,
            generatedDietPolicyButton,
            generatedSurgeryCommandButton,
            generatedAutomaticSurgeryButton,
            generatedSubstanceSelectionButton,
            generatedSubstancePolicyButton);
        RefreshHealthDetails();
    }

    public void CycleDietPolicy()
    {
        healthPresenter.CycleDietPolicy(actor);
    }

    public void OpenSurgeryWindow()
    {
        healthPresenter.OpenSurgeryWindow(actor, UI != null ? UI.transform : transform);
    }

    public void ToggleAutomaticEmergencySurgery()
    {
        healthPresenter.ToggleAutomaticEmergencySurgery(actor);
    }

    public void SelectNextSubstance()
    {
        healthPresenter.SelectNextSubstance(actor);
    }

    public void CycleSelectedSubstancePolicy()
    {
        healthPresenter.CycleSelectedSubstancePolicy(actor);
    }

    public void ExecuteCaptivityAction()
    {
        healthPresenter.ExecuteCaptivityAction(actor);
    }

    public void BindGeneratedCombat(
        TMP_Text generatedCombatSummaryText,
        GameObject generatedCombatTabContent,
        Button generatedCombatTabButton,
        Button generatedLoadoutButton,
        Button generatedWeaponButton,
        Button generatedReloadButton,
        Button generatedFireModeButton,
        Button generatedHoldFireButton,
        Button generatedRepairButton)
    {
        combatTabContent = generatedCombatTabContent;
        combatTabButton = generatedCombatTabButton;
        combatPresenter.Bind(
            generatedCombatSummaryText,
            generatedLoadoutButton,
            generatedWeaponButton,
            generatedReloadButton,
            generatedFireModeButton,
            generatedHoldFireButton,
            generatedRepairButton);
        RefreshCombatDetails();
    }

    public void BindGeneratedGrowth(
        Slider generatedExperience,
        TMP_Text generatedSummary,
        Button[] generatedSkillButtons)
    {
        growthPresenter.Bind(generatedExperience, generatedSummary, generatedSkillButtons);
        RefreshProgression();
    }

    public void BindGeneratedDetailedStats(
        Button entryButton,
        GameObject panel,
        TMP_Text title,
        TMP_Text content,
        Button[] tabButtons)
    {
        shellPresenter.BindDetailedStats(entryButton, panel, title, content, tabButtons);
    }

    public void OpenDetailedStats()
    {
        shellPresenter.OpenDetailedStats(actor);
    }

    public void CloseDetailedStats()
    {
        shellPresenter.CloseDetailedStats();
    }

    public void ShowDetailedStatsTab(CharacterDetailedStatsTab tab)
    {
        shellPresenter.ShowDetailedStatsTab(actor, tab);
    }

    private void RefreshDetailedStats()
    {
        shellPresenter.RefreshDetailedStats(actor);
    }

    public void ShowStatusTab()
    {
        SetActiveTab(CharacterSummaryTab.Status);
    }

    public void ShowMoodTab()
    {
        SetActiveTab(CharacterSummaryTab.Mood);
        RefreshMoodDetails();
    }

    public void ShowHealthTab()
    {
        SetActiveTab(CharacterSummaryTab.Health);
        RefreshHealthDetails();
    }

    public void ShowGrowthTab()
    {
        SetActiveTab(CharacterSummaryTab.Growth);
        RefreshProgression();
    }

    public void ShowRecordsTab()
    {
        SetActiveTab(CharacterSummaryTab.Records);
        RefreshLogText();
    }

    public void ShowAiTab()
    {
        SetActiveTab(CharacterSummaryTab.Ai);
        RefreshAiDetails();
    }

    public void ShowCombatTab()
    {
        SetActiveTab(CharacterSummaryTab.Combat);
        RefreshCombatDetails();
    }

    public void ToggleCombatLoadout()
    {
        combatPresenter.ToggleLoadout(actor);
    }

    public void CycleCombatWeapon()
    {
        combatPresenter.CycleWeapon(actor);
    }

    public void ReloadCombatWeapon()
    {
        combatPresenter.Reload(actor);
    }

    public void CycleCombatFireMode()
    {
        combatPresenter.CycleFireMode(actor);
    }

    public void ToggleCombatHoldFire()
    {
        combatPresenter.ToggleHoldFire(actor);
    }

    public void RequestCombatEquipmentRepair()
    {
        combatPresenter.RequestRepair(actor);
    }

    private void SetActiveTab(CharacterSummaryTab tab)
    {
        if (statusTabContent != null)
        {
            statusTabContent.SetActive(tab == CharacterSummaryTab.Status);
        }

        if (growthTabContent != null)
        {
            growthTabContent.SetActive(tab == CharacterSummaryTab.Growth);
        }

        if (healthTabContent != null)
        {
            healthTabContent.SetActive(tab == CharacterSummaryTab.Health);
        }

        if (moodTabContent != null)
        {
            moodTabContent.SetActive(tab == CharacterSummaryTab.Mood);
        }

        if (recordsTabContent != null)
        {
            recordsTabContent.SetActive(tab == CharacterSummaryTab.Records);
        }

        if (aiTabContent != null)
        {
            aiTabContent.SetActive(tab == CharacterSummaryTab.Ai);
        }

        if (combatTabContent != null)
        {
            combatTabContent.SetActive(tab == CharacterSummaryTab.Combat);
        }

        DungeonUiTheme.StyleButton(statusTabButton, selected: tab == CharacterSummaryTab.Status);
        DungeonUiTheme.StyleButton(healthTabButton, selected: tab == CharacterSummaryTab.Health);
        DungeonUiTheme.StyleButton(growthTabButton, selected: tab == CharacterSummaryTab.Growth);
        DungeonUiTheme.StyleButton(moodTabButton, selected: tab == CharacterSummaryTab.Mood);
        DungeonUiTheme.StyleButton(recordsTabButton, selected: tab == CharacterSummaryTab.Records);
        DungeonUiTheme.StyleButton(aiTabButton, selected: tab == CharacterSummaryTab.Ai);
        DungeonUiTheme.StyleButton(combatTabButton, selected: tab == CharacterSummaryTab.Combat);
    }

    public void ToggleSkillAt(int index)
    {
        growthPresenter.ToggleSkill(actor, progression, index);
    }

    public void OnTriggerEvent(CharacterGrowthTabRequestedEvent eventType)
    {
        if (eventType.Actor == null)
        {
            return;
        }

        if (actor != eventType.Actor)
        {
            OnTriggerEvent(new InfoFeedEvent(eventType.Actor));
        }

        ShowGrowthTab();
    }

    public void OnLogAdded(CharacterLogEntry entry)
    {
        RefreshLogText();
    }

    public void RefreshLogText()
    {
        if (logText == null)
        {
            return;
        }

        logText.text = CharacterSummaryTextFormatter.FormatLogText(characterLog, 40);
    }

    public void RefreshProgression()
    {
        growthPresenter.Refresh(actor, progression);
    }

    public void RefreshMoodDetails()
    {
        if (characterStats == null)
        {
            SetMeter(mood, 0f, "--");
            if (moodSummaryText != null)
            {
                moodSummaryText.text = "기분 정보가 없습니다.";
            }

            if (moodFactorsText != null)
            {
                moodFactorsText.text = "적용 중인 요인이 없습니다.";
            }

            return;
        }

        CharacterMoodSnapshot snapshot = characterStats.GetMoodSnapshot();
        SetMeter(mood, snapshot.Value / 100f, $"{Mathf.RoundToInt(snapshot.Value)}");
        if (moodSummaryText != null)
        {
            string offset = snapshot.TotalOffset >= 0f
                ? $"+{Mathf.RoundToInt(snapshot.TotalOffset)}"
                : $"{Mathf.RoundToInt(snapshot.TotalOffset)}";
            moodSummaryText.text =
                $"{CharacterMoodRules.GetMoodLabel(snapshot.Value)} · 기준 {Mathf.RoundToInt(snapshot.BaseValue)} · 보정 {offset}";
        }

        if (moodFactorsText != null)
        {
            moodFactorsText.text = CharacterSummaryTextFormatter.FormatMoodFactors(snapshot);
        }
    }

    public void RefreshAiDetails()
    {
        aiPresenter.Refresh(actor);
    }

    public void RefreshCombatDetails()
    {
        combatPresenter.Refresh(actor);
    }

    public void RefreshHealthDetails()
    {
        healthPresenter.Refresh(actor);
    }

    public void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        UnbindCharacter();
        growthTabRequestedSubscription?.Dispose();
        growthTabRequestedSubscription = null;
        infoFeedSubscription?.Dispose();
        infoFeedSubscription = null;
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        growthTabRequestedSubscription ??=
            gameEventBus.Subscribe<CharacterGrowthTabRequestedEvent>(OnTriggerEvent);
        infoFeedSubscription ??=
            gameEventBus.Subscribe<InfoFeedEvent>(OnTriggerEvent);
    }

    private void RefreshProfileAndVitals()
    {
        statusPresenter.RefreshProfileAndVitals(actor, characterStats);
    }

    private void RefreshCarrySummary()
    {
        statusPresenter.RefreshCarrySummary(actor);
    }

    private void UnbindCharacter()
    {
        if (actor == null && characterStats == null && characterLog == null)
        {
            return;
        }

        if (characterStats != null)
        {
            characterStats.OnStatsInvalidated -= RefreshStatSliders;
        }

        if (characterLog != null)
        {
            characterLog.OnLogAdded -= OnLogAdded;
            characterLog.OnLogDisplayChanged -= RefreshLogText;
        }

        if (progression != null)
        {
            progression.Changed -= RefreshProgression;
        }

        actor = null;
        characterStats = null;
        characterLog = null;
        progression = null;
        growthPresenter.ResetSelection();
        nextVitalsRefreshAt = 0f;
    }

    private GameObject RequireUiRoot()
    {
        if (UI == null)
        {
            throw new InvalidOperationException($"{nameof(CharacterSummaryInfo)} requires a UI root reference.");
        }

        return UI;
    }

    private CharacterSummaryViewActions RequireViewActions()
    {
        viewActions ??= new CharacterSummaryViewActions(
            new CharacterSummaryPopupActions(
                RequestClose,
                OpenDetailedStats,
                CloseDetailedStats,
                ShowDetailedStatsTab),
            new CharacterSummaryTabActions(
                ShowStatusTab,
                ShowHealthTab,
                ShowCombatTab,
                ShowGrowthTab,
                ShowMoodTab,
                ShowRecordsTab,
                ShowAiTab),
            new CharacterSummaryHealthActions(
                ExecuteCaptivityAction,
                CycleDietPolicy,
                OpenSurgeryWindow,
                ToggleAutomaticEmergencySurgery,
                SelectNextSubstance,
                CycleSelectedSubstancePolicy),
            new CharacterSummaryCombatActions(
                ToggleCombatLoadout,
                CycleCombatWeapon,
                ReloadCombatWeapon,
                CycleCombatFireMode,
                ToggleCombatHoldFire,
                RequestCombatEquipmentRepair),
            new CharacterSummaryGrowthActions(ToggleSkillAt));
        return viewActions;
    }

    private static void SetSlider(
        Slider slider,
        IReadOnlyDictionary<CharacterCondition, float> stats,
        CharacterCondition condition)
    {
        if (slider == null || stats == null)
        {
            return;
        }

        float rawValue = stats.TryGetValue(condition, out float value) ? value : 0f;
        SetMeter(slider, rawValue / 100f, $"{Mathf.RoundToInt(rawValue)}");
    }

    private static void SetSlider(
        Slider slider,
        IDictionary<CharacterCondition, float> stats,
        CharacterCondition condition)
    {
        if (slider == null || stats == null)
        {
            return;
        }

        float rawValue = stats.TryGetValue(condition, out float value) ? value : 0f;
        SetMeter(slider, rawValue / 100f, $"{Mathf.RoundToInt(rawValue)}");
    }

    private static void SetMeter(Slider slider, float normalizedValue, string valueText)
    {
        if (slider == null)
        {
            return;
        }

        float clamped = Mathf.Clamp01(normalizedValue);
        slider.value = clamped;
        Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        if (fill != null)
        {
            fill.color = DungeonUiTheme.GetMeterColor(clamped);
        }

        Transform row = slider.transform.parent;
        TMP_Text value = row != null ? row.Find("Value")?.GetComponent<TMP_Text>() : null;
        if (value != null)
        {
            value.text = valueText;
            value.color = clamped < 0.25f
                ? DungeonUiTheme.Danger
                : clamped < 0.5f
                    ? DungeonUiTheme.Warning
                    : DungeonUiTheme.TextSecondary;
        }
    }

    private static string GetDisplayName(GameObject targetObject)
    {
        CharacterIdentity identity = targetObject != null ? targetObject.GetComponent<CharacterIdentity>() : null;
        if (!string.IsNullOrWhiteSpace(identity != null ? identity.DisplayName : null))
        {
            return identity.DisplayName;
        }

        return targetObject != null ? targetObject.name : string.Empty;
    }

    private enum CharacterSummaryTab
    {
        Status,
        Health,
        Combat,
        Growth,
        Mood,
        Records,
        Ai
    }
}
