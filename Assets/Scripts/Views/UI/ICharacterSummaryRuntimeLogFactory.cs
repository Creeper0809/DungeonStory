using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface ICharacterSummaryGeneratedView
{
    void BindGeneratedView(
        TMP_Text nameText,
        TMP_Text profileText,
        Slider healthSlider,
        Slider moodSlider,
        Slider funSlider,
        Slider hungerSlider,
        Slider sleepSlider,
        Slider excretionSlider,
        Slider hygieneSlider,
        TMP_Text moodSummaryText,
        TMP_Text moodFactorsText,
        TMP_Text aiSummaryText,
        TMP_Text carrySummaryText,
        TMP_Text logText);

    void BindGeneratedTabs(
        GameObject statusTabContent,
        GameObject growthTabContent,
        GameObject moodTabContent,
        GameObject recordsTabContent,
        GameObject aiTabContent,
        Button statusTabButton,
        Button growthTabButton,
        Button moodTabButton,
        Button recordsTabButton,
        Button aiTabButton);

    void BindGeneratedSurvival(
        Slider thirst,
        TMP_Text healthSummaryText,
        GameObject healthTabContent,
        Button healthTabButton,
        Button captivityActionButton,
        Button dietPolicyButton,
        Button surgeryCommandButton,
        Button automaticSurgeryButton,
        Button substanceSelectionButton,
        Button substancePolicyButton);

    void BindGeneratedCombat(
        TMP_Text combatSummaryText,
        GameObject combatTabContent,
        Button combatTabButton,
        Button loadoutButton,
        Button weaponButton,
        Button reloadButton,
        Button fireModeButton,
        Button holdFireButton,
        Button repairButton);

    void BindGeneratedPopulation(
        TMP_Text summaryText,
        GameObject tabContent,
        Button tabButton,
        Button globalPolicyButton,
        Button characterPermissionButton);

    void BindGeneratedGrowth(
        Slider experience,
        TMP_Text summary,
        Button[] skillButtons);

    void BindGeneratedDetailedStats(
        Button entryButton,
        GameObject panel,
        TMP_Text title,
        TMP_Text content,
        Button[] tabButtons);
}

public sealed class CharacterSummaryPopupActions
{
    private readonly Action requestClose;
    private readonly Action openDetailedStats;
    private readonly Action closeDetailedStats;
    private readonly Action<CharacterDetailedStatsTab> showDetailedStatsTab;

    public CharacterSummaryPopupActions(
        Action requestClose,
        Action openDetailedStats,
        Action closeDetailedStats,
        Action<CharacterDetailedStatsTab> showDetailedStatsTab)
    {
        this.requestClose = requestClose
            ?? throw new ArgumentNullException(nameof(requestClose));
        this.openDetailedStats = openDetailedStats
            ?? throw new ArgumentNullException(nameof(openDetailedStats));
        this.closeDetailedStats = closeDetailedStats
            ?? throw new ArgumentNullException(nameof(closeDetailedStats));
        this.showDetailedStatsTab = showDetailedStatsTab
            ?? throw new ArgumentNullException(nameof(showDetailedStatsTab));
    }

    public void RequestClose() => requestClose();
    public void OpenDetailedStats() => openDetailedStats();
    public void CloseDetailedStats() => closeDetailedStats();
    public void ShowDetailedStatsTab(CharacterDetailedStatsTab tab) =>
        showDetailedStatsTab(tab);
}

public sealed class CharacterSummaryTabActions
{
    private readonly Action showStatus;
    private readonly Action showHealth;
    private readonly Action showCombat;
    private readonly Action showGrowth;
    private readonly Action showMood;
    private readonly Action showRecords;
    private readonly Action showAi;
    private readonly Action showPopulation;

    public CharacterSummaryTabActions(
        Action showStatus,
        Action showHealth,
        Action showCombat,
        Action showGrowth,
        Action showMood,
        Action showRecords,
        Action showAi,
        Action showPopulation)
    {
        this.showStatus = showStatus
            ?? throw new ArgumentNullException(nameof(showStatus));
        this.showHealth = showHealth
            ?? throw new ArgumentNullException(nameof(showHealth));
        this.showCombat = showCombat
            ?? throw new ArgumentNullException(nameof(showCombat));
        this.showGrowth = showGrowth
            ?? throw new ArgumentNullException(nameof(showGrowth));
        this.showMood = showMood
            ?? throw new ArgumentNullException(nameof(showMood));
        this.showRecords = showRecords
            ?? throw new ArgumentNullException(nameof(showRecords));
        this.showAi = showAi ?? throw new ArgumentNullException(nameof(showAi));
        this.showPopulation = showPopulation
            ?? throw new ArgumentNullException(nameof(showPopulation));
    }

    public void ShowStatus() => showStatus();
    public void ShowHealth() => showHealth();
    public void ShowCombat() => showCombat();
    public void ShowGrowth() => showGrowth();
    public void ShowMood() => showMood();
    public void ShowRecords() => showRecords();
    public void ShowAi() => showAi();
    public void ShowPopulation() => showPopulation();
}

public sealed class CharacterSummaryPopulationActions
{
    private readonly Action toggleGlobalApprenticeship;
    private readonly Action toggleCharacterApprenticeship;

    public CharacterSummaryPopulationActions(
        Action toggleGlobalApprenticeship,
        Action toggleCharacterApprenticeship)
    {
        this.toggleGlobalApprenticeship = toggleGlobalApprenticeship
            ?? throw new ArgumentNullException(nameof(toggleGlobalApprenticeship));
        this.toggleCharacterApprenticeship = toggleCharacterApprenticeship
            ?? throw new ArgumentNullException(nameof(toggleCharacterApprenticeship));
    }

    public void ToggleGlobalApprenticeship() =>
        toggleGlobalApprenticeship();
    public void ToggleCharacterApprenticeship() =>
        toggleCharacterApprenticeship();
}

public sealed class CharacterSummaryHealthActions
{
    private readonly Action executeCaptivityAction;
    private readonly Action cycleDietPolicy;
    private readonly Action openSurgeryWindow;
    private readonly Action toggleAutomaticEmergencySurgery;
    private readonly Action selectNextSubstance;
    private readonly Action cycleSelectedSubstancePolicy;

    public CharacterSummaryHealthActions(
        Action executeCaptivityAction,
        Action cycleDietPolicy,
        Action openSurgeryWindow,
        Action toggleAutomaticEmergencySurgery,
        Action selectNextSubstance,
        Action cycleSelectedSubstancePolicy)
    {
        this.executeCaptivityAction = executeCaptivityAction
            ?? throw new ArgumentNullException(nameof(executeCaptivityAction));
        this.cycleDietPolicy = cycleDietPolicy
            ?? throw new ArgumentNullException(nameof(cycleDietPolicy));
        this.openSurgeryWindow = openSurgeryWindow
            ?? throw new ArgumentNullException(nameof(openSurgeryWindow));
        this.toggleAutomaticEmergencySurgery = toggleAutomaticEmergencySurgery
            ?? throw new ArgumentNullException(
                nameof(toggleAutomaticEmergencySurgery));
        this.selectNextSubstance = selectNextSubstance
            ?? throw new ArgumentNullException(nameof(selectNextSubstance));
        this.cycleSelectedSubstancePolicy = cycleSelectedSubstancePolicy
            ?? throw new ArgumentNullException(
                nameof(cycleSelectedSubstancePolicy));
    }

    public void ExecuteCaptivityAction() => executeCaptivityAction();
    public void CycleDietPolicy() => cycleDietPolicy();
    public void OpenSurgeryWindow() => openSurgeryWindow();
    public void ToggleAutomaticEmergencySurgery() =>
        toggleAutomaticEmergencySurgery();
    public void SelectNextSubstance() => selectNextSubstance();
    public void CycleSelectedSubstancePolicy() =>
        cycleSelectedSubstancePolicy();
}

public sealed class CharacterSummaryCombatActions
{
    private readonly Action toggleLoadout;
    private readonly Action cycleWeapon;
    private readonly Action reload;
    private readonly Action cycleFireMode;
    private readonly Action toggleHoldFire;
    private readonly Action requestRepair;

    public CharacterSummaryCombatActions(
        Action toggleLoadout,
        Action cycleWeapon,
        Action reload,
        Action cycleFireMode,
        Action toggleHoldFire,
        Action requestRepair)
    {
        this.toggleLoadout = toggleLoadout
            ?? throw new ArgumentNullException(nameof(toggleLoadout));
        this.cycleWeapon = cycleWeapon
            ?? throw new ArgumentNullException(nameof(cycleWeapon));
        this.reload = reload ?? throw new ArgumentNullException(nameof(reload));
        this.cycleFireMode = cycleFireMode
            ?? throw new ArgumentNullException(nameof(cycleFireMode));
        this.toggleHoldFire = toggleHoldFire
            ?? throw new ArgumentNullException(nameof(toggleHoldFire));
        this.requestRepair = requestRepair
            ?? throw new ArgumentNullException(nameof(requestRepair));
    }

    public void ToggleLoadout() => toggleLoadout();
    public void CycleWeapon() => cycleWeapon();
    public void Reload() => reload();
    public void CycleFireMode() => cycleFireMode();
    public void ToggleHoldFire() => toggleHoldFire();
    public void RequestRepair() => requestRepair();
}

public sealed class CharacterSummaryGrowthActions
{
    private readonly Action<int> toggleSkill;

    public CharacterSummaryGrowthActions(Action<int> toggleSkill)
    {
        this.toggleSkill = toggleSkill
            ?? throw new ArgumentNullException(nameof(toggleSkill));
    }

    public void ToggleSkillAt(int index) => toggleSkill(index);
}

public sealed class CharacterSummaryViewActions
{
    public CharacterSummaryViewActions(
        CharacterSummaryPopupActions popup,
        CharacterSummaryTabActions tabs,
        CharacterSummaryHealthActions health,
        CharacterSummaryCombatActions combat,
        CharacterSummaryGrowthActions growth,
        CharacterSummaryPopulationActions population)
    {
        Popup = popup ?? throw new ArgumentNullException(nameof(popup));
        Tabs = tabs ?? throw new ArgumentNullException(nameof(tabs));
        Health = health ?? throw new ArgumentNullException(nameof(health));
        Combat = combat ?? throw new ArgumentNullException(nameof(combat));
        Growth = growth ?? throw new ArgumentNullException(nameof(growth));
        Population = population
            ?? throw new ArgumentNullException(nameof(population));
    }

    public CharacterSummaryPopupActions Popup { get; }
    public CharacterSummaryTabActions Tabs { get; }
    public CharacterSummaryHealthActions Health { get; }
    public CharacterSummaryCombatActions Combat { get; }
    public CharacterSummaryGrowthActions Growth { get; }
    public CharacterSummaryPopulationActions Population { get; }
}

public interface ICharacterSummaryRuntimeLogFactory
{
    void Ensure(
        ICharacterSummaryGeneratedView view,
        CharacterSummaryViewActions actions,
        GameObject uiRoot);
    void ApplyFonts(Transform root);
}
