using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class CharacterSummeryInfo : UIPopUp
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
    private TMP_Text profileText;
    private Slider health;
    private Slider experience;
    private TMP_Text progressionSummaryText;
    private Button[] skillButtons = Array.Empty<Button>();
    private TMP_Text moodSummaryText;
    private TMP_Text moodFactorsText;
    private TMP_Text aiSummaryText;
    private TMP_Text carrySummaryText;
    private TMP_Text healthSummaryText;
    private TMP_Text combatSummaryText;
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
    private Button combatLoadoutButton;
    private Button combatWeaponButton;
    private Button combatReloadButton;
    private Button combatFireModeButton;
    private Button combatHoldFireButton;
    private Button combatRepairButton;
    private Button captivityActionButton;
    private float nextVitalsRefreshAt;
    private int pendingCandidateConfirmation = -1;
    private int pendingCandidateUnlockLevel = -1;
    private IUiPopupService popupService;
    private ICharacterSummaryRuntimeLogFactory runtimeLogFactory;
    private IExpeditionEquipmentRuntime equipmentRuntime;
    private IDungeonItemCatalogProvider itemCatalogProvider;
    private IItemHaulingSettingsProvider haulingSettingsProvider;
    private ICharacterBodyHealthRuntime bodyHealthRuntime;
    private ICharacterDeprivationRuntime deprivationRuntime;
    private IDefenseEngagementRuntime defenseEngagementRuntime;
    private ICombatEquipmentRuntime combatEquipmentRuntime;
    private ICombatEquipmentCatalog combatEquipmentCatalog;
    private ICombatEquipmentMaintenanceRuntime equipmentMaintenanceRuntime;
    private ISurvivalFoodRuntime survivalFoodRuntime;
    private ICharacterAiDiagnosticsQuery aiDiagnostics;
    private ICaptivityRuntime captivityRuntime;
    private ICaptivityCommandService captivityCommands;
    private ICharacterAiWorldRegistry characterWorld;
    private IUiClock uiClock;
    private IGameEventBus gameEventBus;
    private IDisposable growthTabRequestedSubscription;
    private IDisposable infoFeedSubscription;

    [Inject]
    public void Construct(
        IUiPopupService popupService,
        ICharacterSummaryRuntimeLogFactory runtimeLogFactory,
        IDungeonItemCatalogProvider itemCatalogProvider,
        IItemHaulingSettingsProvider haulingSettingsProvider,
        ICharacterBodyHealthRuntime bodyHealthRuntime,
        ICharacterDeprivationRuntime deprivationRuntime,
        IDefenseEngagementRuntime defenseEngagementRuntime,
        ICombatEquipmentRuntime combatEquipmentRuntime,
        ICombatEquipmentCatalog combatEquipmentCatalog,
        ICombatEquipmentMaintenanceRuntime equipmentMaintenanceRuntime,
        ISurvivalFoodRuntime survivalFoodRuntime,
        IExpeditionEquipmentRuntime equipmentRuntime,
        ICharacterAiDiagnosticsQuery aiDiagnostics,
        ICaptivityRuntime captivityRuntime,
        ICaptivityCommandService captivityCommands,
        ICharacterAiWorldRegistry characterWorld,
        IUiClock uiClock,
        IGameEventBus gameEventBus)
    {
        this.popupService = popupService
            ?? throw new ArgumentNullException(nameof(popupService));
        this.runtimeLogFactory = runtimeLogFactory
            ?? throw new ArgumentNullException(nameof(runtimeLogFactory));
        this.itemCatalogProvider = itemCatalogProvider
            ?? throw new ArgumentNullException(nameof(itemCatalogProvider));
        this.haulingSettingsProvider = haulingSettingsProvider
            ?? throw new ArgumentNullException(nameof(haulingSettingsProvider));
        this.bodyHealthRuntime = bodyHealthRuntime
            ?? throw new ArgumentNullException(nameof(bodyHealthRuntime));
        this.deprivationRuntime = deprivationRuntime
            ?? throw new ArgumentNullException(nameof(deprivationRuntime));
        this.defenseEngagementRuntime = defenseEngagementRuntime
            ?? throw new ArgumentNullException(nameof(defenseEngagementRuntime));
        this.combatEquipmentRuntime = combatEquipmentRuntime
            ?? throw new ArgumentNullException(nameof(combatEquipmentRuntime));
        this.combatEquipmentCatalog = combatEquipmentCatalog
            ?? throw new ArgumentNullException(nameof(combatEquipmentCatalog));
        this.equipmentMaintenanceRuntime = equipmentMaintenanceRuntime
            ?? throw new ArgumentNullException(nameof(equipmentMaintenanceRuntime));
        this.survivalFoodRuntime = survivalFoodRuntime
            ?? throw new ArgumentNullException(nameof(survivalFoodRuntime));
        this.equipmentRuntime = equipmentRuntime
            ?? throw new ArgumentNullException(nameof(equipmentRuntime));
        this.aiDiagnostics = aiDiagnostics
            ?? throw new ArgumentNullException(nameof(aiDiagnostics));
        this.captivityRuntime = captivityRuntime
            ?? throw new ArgumentNullException(nameof(captivityRuntime));
        this.captivityCommands = captivityCommands
            ?? throw new ArgumentNullException(nameof(captivityCommands));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.uiClock = uiClock
            ?? throw new ArgumentNullException(nameof(uiClock));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToScopedEvents();
    }

    private void Start()
    {
        GameObject uiRoot = RequireUiRoot();
        ICharacterSummaryRuntimeLogFactory viewFactory = ResolveRuntimeLogFactory();
        viewFactory.Ensure(this, uiRoot);
        viewFactory.ApplyFonts(uiRoot.transform);
        uiRoot.SetActive(false);
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
    }

    public void OnTriggerEvent(InfoFeedEvent eventType)
    {
        if (eventType.Target is not CharacterActor nextActor || nextActor == null)
        {
            return;
        }

        ResolvePopupService().CloseAll();
        UnbindCharacter();

        actor = nextActor;
        GameObject targetObject = actor.gameObject;
        characterStats = targetObject.GetComponent<CharacterStats>();
        characterLog = targetObject.GetComponent<CharacterLog>();
        progression = actor.Progression;

        GameObject uiRoot = RequireUiRoot();
        ICharacterSummaryRuntimeLogFactory viewFactory = ResolveRuntimeLogFactory();
        viewFactory.Ensure(this, uiRoot);
        viewFactory.ApplyFonts(uiRoot.transform);
        uiRoot.SetActive(true);
        ResolvePopupService().Open(this);
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
            OnStatChange(characterStats.StatSnapshot);
            characterStats.OnStatChange += OnStatChange;
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
        RequireUiRoot().SetActive(false);
        UnbindCharacter();
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
        profileText = generatedProfileText;
        health = healthSlider;
        mood = moodSlider;
        fun = funSlider;
        hunger = hungerSlider;
        sleep = sleepSlider;
        excretion = excretionSlider;
        hygiene = hygieneSlider;
        moodSummaryText = generatedMoodSummaryText;
        moodFactorsText = generatedMoodFactorsText;
        aiSummaryText = generatedAiSummaryText;
        carrySummaryText = generatedCarrySummaryText;
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
        Button generatedCaptivityActionButton)
    {
        thirst = generatedThirst;
        healthSummaryText = generatedHealthSummaryText;
        healthTabContent = generatedHealthTabContent;
        healthTabButton = generatedHealthTabButton;
        captivityActionButton = generatedCaptivityActionButton;
        RefreshHealthDetails();
    }

    public void ExecuteCaptivityAction()
    {
        if (actor == null || captivityRuntime == null || captivityCommands == null)
        {
            return;
        }

        string captiveId = actor.Identity?.PersistentId ?? string.Empty;
        if (!captivityRuntime.TryGetCaptive(captiveId, out CaptiveState captive)
            || !captive.IsActive)
        {
            ShowCaptivityNotice(
                "이 대상은 포획할 수 있는 쓰러진 침입자가 아닙니다.",
                false);
            return;
        }

        if (captive.status == CaptivityStatus.AwaitingCapture)
        {
            CharacterActor carrier = characterWorld.AllCharacters
                .Where(candidate => IsAvailableCaptureCarrier(candidate, captiveId))
                .OrderBy(candidate => Manhattan(
                    candidate.GetNowXY(),
                    actor.GetNowXY()))
                .FirstOrDefault();
            string captureFailure = string.Empty;
            bool started = carrier != null
                && captivityCommands.TryOrderCapture(
                    actor,
                    carrier,
                    out captureFailure);
            string message = carrier == null
                ? "포로를 운반할 수 있는 직원이 없습니다."
                : started
                    ? $"{carrier.Identity?.DisplayName ?? carrier.name}에게 포획과 호송을 명령했습니다."
                    : captureFailure;
            ShowCaptivityNotice(message, started);
        }
        else if (captive.status == CaptivityStatus.Confined)
        {
            bool started = captivityCommands.TrySetLaborPermissions(
                captiveId,
                CaptiveLaborPermission.Clean | CaptiveLaborPermission.Haul,
                out string reason);
            ShowCaptivityNotice(
                started ? "청소·운반 노역을 허용했습니다." : reason,
                started);
        }
        else if (captive.status == CaptivityStatus.Labor)
        {
            bool stopped = captivityCommands.TrySetLaborPermissions(
                captiveId,
                CaptiveLaborPermission.None,
                out string reason);
            ShowCaptivityNotice(
                stopped ? "노역을 중지하고 감방으로 돌려보냈습니다." : reason,
                stopped);
        }
        else
        {
            ShowCaptivityNotice(
                "세부 처우는 운영 탭의 포로·노역 항목에서 관리할 수 있습니다.",
                true);
        }

        RefreshHealthDetails();
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
        combatSummaryText = generatedCombatSummaryText;
        combatTabContent = generatedCombatTabContent;
        combatTabButton = generatedCombatTabButton;
        combatLoadoutButton = generatedLoadoutButton;
        combatWeaponButton = generatedWeaponButton;
        combatReloadButton = generatedReloadButton;
        combatFireModeButton = generatedFireModeButton;
        combatHoldFireButton = generatedHoldFireButton;
        combatRepairButton = generatedRepairButton;
        RefreshCombatDetails();
    }

    public void BindGeneratedGrowth(
        Slider generatedExperience,
        TMP_Text generatedSummary,
        Button[] generatedSkillButtons)
    {
        experience = generatedExperience;
        progressionSummaryText = generatedSummary;
        skillButtons = generatedSkillButtons ?? Array.Empty<Button>();
        RefreshProgression();
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
        if (!TryGetCombatRuntime(out ICombatEquipmentRuntime equipment, out string characterId))
        {
            return;
        }

        CharacterCombatLoadoutProfile current = equipment.GetActiveProfileSnapshot(characterId);
        string target = string.Equals(
            current?.profileId,
            CombatLoadoutPresetIds.Peace,
            StringComparison.Ordinal)
            ? CombatLoadoutPresetIds.Combat
            : CombatLoadoutPresetIds.Peace;
        bool success = equipment.TrySetActiveProfile(characterId, target);
        gameEventBus.ShowNotice(
            success
                ? target == CombatLoadoutPresetIds.Combat
                    ? $"{actor.name}: 전투 로드아웃으로 전환"
                    : $"{actor.name}: 평시 로드아웃으로 전환"
                : "현재 장비 조합으로는 해당 로드아웃을 사용할 수 없습니다.",
            success ? NoticeFeedEvent.Grade.NONE : NoticeFeedEvent.Grade.WARNING);
        RefreshCombatDetails();
    }

    public void CycleCombatWeapon()
    {
        if (!TryGetCombatRuntime(out ICombatEquipmentRuntime equipment, out string characterId))
        {
            return;
        }

        CharacterCombatLoadoutProfile profile = equipment.GetActiveProfileSnapshot(characterId);
        string[] weaponIds = profile?.weaponInstanceIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray()
            ?? Array.Empty<string>();
        if (weaponIds.Length == 0)
        {
            gameEventBus.ShowNotice("교체할 소지 무기가 없습니다.", NoticeFeedEvent.Grade.WARNING);
            return;
        }

        int currentIndex = Array.FindIndex(
            weaponIds,
            id => string.Equals(id, profile.activeWeaponInstanceId, StringComparison.Ordinal));
        for (int offset = 1; offset <= weaponIds.Length; offset++)
        {
            string candidate = weaponIds[(Mathf.Max(-1, currentIndex) + offset) % weaponIds.Length];
            if (equipment.TrySetActiveWeapon(characterId, candidate, out string failureReason))
            {
                gameEventBus.ShowNotice(
                    $"{actor.name}: 무기 교체",
                    NoticeFeedEvent.Grade.NONE);
                RefreshCombatDetails();
                return;
            }

            if (offset == weaponIds.Length)
            {
                gameEventBus.ShowNotice(failureReason, NoticeFeedEvent.Grade.WARNING);
            }
        }
    }

    public void ReloadCombatWeapon()
    {
        if (!TryGetCombatRuntime(out ICombatEquipmentRuntime equipment, out string characterId)
            || !equipment.TryGetActiveWeapon(characterId, out CombatWeaponSnapshot weapon)
            || weapon == null
            || string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            gameEventBus.ShowNotice("재장전할 활성 무기가 없습니다.", NoticeFeedEvent.Grade.WARNING);
            return;
        }

        bool success = equipment.TryReloadFromCharacterInventory(
            characterId,
            weapon.InstanceId,
            out int consumed);
        gameEventBus.ShowNotice(
            success
                ? $"{actor.name}: 탄약 {consumed}발 재장전"
                : "맞는 탄약이 없거나 이미 장전되어 있습니다.",
            success ? NoticeFeedEvent.Grade.NONE : NoticeFeedEvent.Grade.WARNING);
        RefreshCombatDetails();
    }

    public void CycleCombatFireMode()
    {
        if (!TryGetCombatRuntime(out ICombatEquipmentRuntime equipment, out string characterId))
        {
            return;
        }

        CharacterCombatLoadoutProfile profile = equipment.GetActiveProfileSnapshot(characterId);
        CombatFireMode[] modes =
        {
            CombatFireMode.Aimed,
            CombatFireMode.Rapid,
            CombatFireMode.Suppressive
        };
        int currentIndex = Array.IndexOf(modes, profile?.fireMode ?? CombatFireMode.Aimed);
        string lastFailure = "사용 가능한 사격 모드가 없습니다.";
        for (int offset = 1; offset <= modes.Length; offset++)
        {
            CombatFireMode candidate = modes[(Mathf.Max(0, currentIndex) + offset) % modes.Length];
            if (equipment.TrySetFireMode(characterId, candidate, out lastFailure))
            {
                gameEventBus.ShowNotice(
                    $"{actor.name}: {FormatCombatFireMode(candidate)} 사격",
                    NoticeFeedEvent.Grade.NONE);
                RefreshCombatDetails();
                return;
            }
        }

        gameEventBus.ShowNotice(lastFailure, NoticeFeedEvent.Grade.WARNING);
    }

    public void ToggleCombatHoldFire()
    {
        if (!TryGetCombatRuntime(out ICombatEquipmentRuntime equipment, out string characterId))
        {
            return;
        }

        CharacterCombatLoadoutProfile profile = equipment.GetActiveProfileSnapshot(characterId);
        bool holdFire = !(profile?.holdFire ?? false);
        if (equipment.TrySetHoldFire(characterId, holdFire))
        {
            gameEventBus.ShowNotice(
                holdFire ? $"{actor.name}: 사격 중지" : $"{actor.name}: 사격 허용",
                NoticeFeedEvent.Grade.NONE);
        }

        RefreshCombatDetails();
    }

    public void RequestCombatEquipmentRepair()
    {
        if (!TryGetCombatRuntime(out ICombatEquipmentRuntime equipment, out string characterId)
            || equipmentMaintenanceRuntime == null)
        {
            gameEventBus.ShowNotice(
                "장비 수리 정보를 불러올 수 없습니다.",
                NoticeFeedEvent.Grade.WARNING);
            return;
        }

        CharacterCombatLoadoutProfile profile = equipment.GetActiveProfileSnapshot(characterId);
        IEnumerable<string> candidateIds = (profile?.armorInstanceIds ?? new List<string>())
            .Concat(string.IsNullOrWhiteSpace(profile?.shieldInstanceId)
                ? Array.Empty<string>()
                : new[] { profile.shieldInstanceId });
        CombatEquipmentInstance candidate = candidateIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Select(id => equipment.TryGetInstance(id, out CombatEquipmentInstance instance)
                ? instance
                : null)
            .Where(instance => instance != null && instance.durabilityRatio < 0.999f)
            .OrderBy(instance => instance.durabilityRatio)
            .FirstOrDefault();
        if (candidate == null)
        {
            gameEventBus.ShowNotice(
                "수리가 필요한 방어구나 방패가 없습니다.",
                NoticeFeedEvent.Grade.WARNING);
            return;
        }

        bool requested = equipmentMaintenanceRuntime.TryRequestManualRepair(
            candidate.instanceId,
            out string message);
        gameEventBus.ShowNotice(
            message,
            requested ? NoticeFeedEvent.Grade.NONE : NoticeFeedEvent.Grade.WARNING);
        RefreshCombatDetails();
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
        if (actor == null || progression == null || index < 7 || index >= 10)
        {
            return;
        }

        CharacterSkillDraft draft = GetCurrentActiveDraft();
        int candidateIndex = index - 7;
        if (draft == null || candidateIndex >= draft.candidates.Count)
        {
            return;
        }

        bool confirmed = pendingCandidateUnlockLevel == draft.unlockLevel
            && pendingCandidateConfirmation == candidateIndex;
        if (!confirmed)
        {
            pendingCandidateUnlockLevel = draft.unlockLevel;
            pendingCandidateConfirmation = candidateIndex;
            gameEventBus.ShowNotice(
                $"{draft.candidates[candidateIndex].displayName}: 다시 누르면 영구 확정",
                NoticeFeedEvent.Grade.WARNING);
        }
        else
        {
            progression.TryChooseActiveSkill(
                draft.unlockLevel,
                candidateIndex,
                confirmed: true,
                out string message);
            gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.NONE);
            pendingCandidateUnlockLevel = -1;
            pendingCandidateConfirmation = -1;
        }

        RefreshProgression();
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

        logText.text = FormatLogText(characterLog, 40);
    }

    public void RefreshProgression()
    {
        if (progression == null || actor == null)
        {
            SetMeter(experience, 0f, "--");
            if (progressionSummaryText != null)
            {
                progressionSummaryText.text = "성장 정보가 없습니다.";
            }

            return;
        }

        string experienceText = progression.Level >= CharacterProgression.MaxLevel
            ? "MAX"
            : $"{progression.CurrentExperience}/{progression.ExperienceToNextLevel}";
        SetMeter(experience, progression.ExperienceRatio, experienceText);
        if (progressionSummaryText != null)
        {
            string traits = string.Join(", ", progression.ResolveSelectedTraits()
                .Where(trait => trait != null)
                .Select(trait => trait.traitName));
            progressionSummaryText.text = BuildProgressionSummary(traits);
        }

        for (int i = 0; i < skillButtons.Length; i++)
        {
            Button button = skillButtons[i];
            if (button == null)
            {
                continue;
            }

            TMP_Text label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
            button.gameObject.SetActive(true);
            button.interactable = false;
            bool selected = false;
            string text;
            if (i == 0)
            {
                CharacterCombatAbilityDefinition species =
                    CharacterCombatAbilityCatalog.GetSpeciesAbilities(actor).FirstOrDefault();
                text = species != null
                    ? $"종족기  {species.DisplayName}  ·  {species.Description}"
                    : "종족기  없음";
            }
            else if (i <= 3)
            {
                int slot = i - 1;
                CharacterSkillInstance skill = slot < progression.ActiveSkills.Count
                    ? progression.ActiveSkills[slot]
                    : null;
                int unlockLevel = new[] { 1, 5, 30 }[slot];
                text = skill != null
                    ? $"액티브 {slot + 1}  [{CharacterSkillDisplay.Rarity(skill.rarity)}] {skill.displayName}  ·  {skill.description}"
                    : $"액티브 {slot + 1}  Lv.{unlockLevel} 선택 대기";
            }
            else if (i <= 5)
            {
                int slot = i - 4;
                CharacterSkillInstance skill = slot < progression.PassiveSkills.Count
                    ? progression.PassiveSkills[slot]
                    : null;
                text = skill != null
                    ? $"패시브 {slot + 1}  [{CharacterSkillDisplay.Rarity(skill.rarity)}] {skill.displayName}  ·  {skill.description}"
                    : $"패시브 {slot + 1}  {(slot == 0 ? "정체성 기술 대기" : "Lv.25 서사 조건")}";
            }
            else if (i == 6)
            {
                CharacterSkillInstance skill = progression.Ultimate;
                text = skill != null
                    ? $"궁극기  [{skill.ultimateDomain}] {skill.displayName}  ·  {skill.description}"
                    : "궁극기  Lv.50 서사 완성 후 획득";
            }
            else
            {
                CharacterSkillDraft draft = GetCurrentActiveDraft();
                int candidateIndex = i - 7;
                CharacterSkillInstance candidate = draft != null && candidateIndex < draft.candidates.Count
                    ? draft.candidates[candidateIndex]
                    : null;
                bool visible = candidate != null;
                button.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                selected = pendingCandidateUnlockLevel == draft.unlockLevel
                    && pendingCandidateConfirmation == candidateIndex;
                text = $"후보 {candidateIndex + 1}  [{CharacterSkillDisplay.Rarity(candidate.rarity)}] {candidate.displayName}  ·  {candidate.description}";
                button.interactable = true;
            }

            if (label != null)
            {
                label.text = text;
            }

            DungeonUiTheme.StyleButton(button, selected);
        }
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
            moodFactorsText.text = FormatMoodFactors(snapshot);
        }
    }

    public void RefreshAiDetails()
    {
        if (aiSummaryText == null)
        {
            return;
        }

        if (actor == null)
        {
            aiSummaryText.text = "AI 정보가 없습니다.";
            return;
        }

        StringBuilder builder = new StringBuilder(1024);
        if (defenseEngagementRuntime != null
            && defenseEngagementRuntime.TryGetActorDefenseStatus(
                actor,
                out DefenseEngagement defenseEngagement,
                out string defenseRole,
                out string defenseStatus))
        {
            builder.AppendLine($"방어 임무  {defenseRole} · {Fallback(defenseStatus, "대기")}");
            builder.AppendLine($"교전 위치  침입자 {defenseEngagement.IntruderStopCell} / 경비 {defenseEngagement.GuardCell}");
            builder.AppendLine($"공방 횟수  {defenseEngagement.ExchangeCount}");
            builder.AppendLine();
        }

        CharacterBlackboard blackboard = actor.Blackboard;
        if (blackboard != null)
        {
            builder.AppendLine($"현재 BT 분기  {CharacterAiUtilityText.GetBranchLabel(blackboard.CurrentBranch)}");
            builder.AppendLine($"현재 의도  {Fallback(blackboard.CurrentIntent, "없음")}");
            builder.AppendLine($"행동 단계  {Fallback(blackboard.CurrentTask, "대기")} · {Fallback(blackboard.CurrentStatus, "판단 대기")}");
            builder.AppendLine($"의도 유지  {blackboard.GetSoftLockDebugSummary()}");

            CharacterAiWorldSignalSnapshot signals = actor.WorldSignalQuery?.Capture(
                    actor,
                    blackboard.CurrentBranch)
                ?? CharacterAiWorldSignalSnapshot.Neutral;
            builder.AppendLine($"주변 신호  {signals.ToCompactString()}");

            if (!string.IsNullOrWhiteSpace(blackboard.LastDecisionContextSummary))
            {
                builder.AppendLine();
                builder.AppendLine(blackboard.LastDecisionContextSummary);
            }

            IReadOnlyList<string> breakdowns = blackboard.TopUtilityBreakdowns;
            if (breakdowns != null && breakdowns.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("상위 후보 5개");
                foreach (string row in breakdowns.Take(5))
                {
                    builder.AppendLine($"- {row}");
                }
            }

            if (!string.IsNullOrWhiteSpace(blackboard.LastCommitBreakReason))
            {
                builder.AppendLine();
                builder.AppendLine($"최근 중단 사유  {blackboard.LastCommitBreakReason}");
            }
        }

        if (actor.Brain != null)
        {
            builder.AppendLine();
            builder.AppendLine("현재 행동");
            builder.AppendLine(actor.Brain.GetDebugSummary(5));
        }

        if (aiDiagnostics != null)
        {
            builder.AppendLine();
            builder.AppendLine(
                $"다음 판단  {aiDiagnostics.GetNextDecisionDelay(actor):0.0}s 후");
            builder.AppendLine(
                $"AI 처리  {aiDiagnostics.LastProcessingMilliseconds:0.00}ms"
                + $" · 경로 {aiDiagnostics.LastPathSearchCount}"
                + $"/{aiDiagnostics.CurrentPathSearchBudget}");
        }

        if (actor.AiMemory != null)
        {
            builder.AppendLine();
            builder.AppendLine("최근 기억");
            builder.AppendLine(actor.AiMemory.GetRecentMemorySummary());
        }

        string text = builder.ToString().TrimEnd();
        aiSummaryText.text = string.IsNullOrWhiteSpace(text)
            ? "아직 AI 판단 기록이 없습니다."
            : text;
    }

    public void RefreshCombatDetails()
    {
        if (combatSummaryText == null)
        {
            return;
        }

        if (actor == null)
        {
            combatSummaryText.text = "전투 정보가 없습니다.";
            return;
        }

        string characterId = actor.Identity?.PersistentId ?? string.Empty;
        int melee = actor.GetCharacterStat(CharacterStatType.Attack);
        int shooting = actor.GetCharacterStat(CharacterStatType.Shooting);
        int evasion = actor.GetCharacterStat(CharacterStatType.Evasion);
        int move = actor.GetCharacterStat(CharacterStatType.MoveSpeed);
        int strength = actor.GetCharacterStat(CharacterStatType.Strength);
        int dexterity = actor.GetCharacterStat(CharacterStatType.Dexterity);

        float baseRangedHit = Mathf.Clamp(
            0.45f + shooting * 0.025f + dexterity * 0.01f,
            0.05f,
            0.95f);
        float baseMeleeHit = Mathf.Clamp(0.72f + (melee + dexterity) * 0.018f, 0.1f, 0.95f);
        float baseEvasion = Mathf.Clamp(0.02f + evasion * 0.01f + move * 0.003f, 0f, 0.35f);

        StringBuilder builder = new StringBuilder(1536);
        builder.AppendLine("전투 능력");
        builder.AppendLine($"근접 {melee} · 사격 {shooting} · 회피 {evasion} · 민첩 {dexterity} · 힘 {strength}");
        builder.AppendLine($"기본 사격 명중 {baseRangedHit * 100f:0.#}%  (45 + 사격×2.5 + 민첩×1)");
        builder.AppendLine($"기본 근접 명중 {baseMeleeHit * 100f:0.#}%  (대상 회피 전)");
        builder.AppendLine($"기본 회피 {baseEvasion * 100f:0.#}%  (2 + 회피×1 + 이동×0.3)");

        CharacterBodyHealthSnapshot body = bodyHealthRuntime.GetSnapshot(actor);
        builder.AppendLine();
        builder.AppendLine("신체 상태");
        builder.AppendLine(
            $"의식 {body.Consciousness * 100f:0}% · 조작 {body.Manipulation * 100f:0}% · 이동 {body.Mobility * 100f:0}%");
        builder.AppendLine(
            $"혈액 손실 {body.BloodLoss:0.#}% · 제압 {body.Suppression:0.#}%"
            + (body.Downed ? " · 쓰러짐" : string.Empty));
        foreach (CharacterBodyPartHealthState part in body.Parts ?? Array.Empty<CharacterBodyPartHealthState>())
        {
            string bleeding = part.bleedingPerSecond > 0f
                ? $" · 출혈 {part.bleedingPerSecond:0.##}/s"
                : string.Empty;
            builder.AppendLine(
                $"- {CombatBodyPartRules.GetDisplayName(part.bodyPart)} "
                + $"{part.currentHealth:0.#}/{part.maxHealth:0.#}{bleeding}");
        }

        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        CharacterCombatLoadoutProfile profile = equipment?.GetActiveProfileSnapshot(characterId);
        ICombatEquipmentCatalog catalog = combatEquipmentCatalog;
        builder.AppendLine();
        builder.AppendLine("장비와 탄약");
        builder.AppendLine($"로드아웃  {profile?.displayName ?? "평시"}");

        if (equipment != null
            && equipment.TryGetActiveWeapon(characterId, out CombatWeaponSnapshot weapon)
            && weapon != null
            && !string.IsNullOrWhiteSpace(weapon.InstanceId)
            && catalog.TryGet(weapon.DefinitionId, out CombatEquipmentDefinitionSO weaponDefinition))
        {
            string ammo = weapon.RequiresAmmo
                ? $" · 장전 {weapon.LoadedAmmo}/{weapon.MagazineCapacity}"
                : string.Empty;
            builder.AppendLine(
                $"활성 무기  {weaponDefinition.DisplayName} [{CombatQualityRules.GetDisplayName(weapon.Quality)}]"
                + $" · 최대 {weapon.MaximumRange}칸{ammo}");
            builder.AppendLine(
                $"사격 모드  {FormatCombatFireMode(profile?.fireMode ?? CombatFireMode.Aimed)}"
                + ((profile?.holdFire ?? false) ? " · 사격 중지" : string.Empty));
        }
        else
        {
            builder.AppendLine("활성 무기  맨손");
        }

        AppendCombatEquipmentList(
            builder,
            "방어구",
            profile?.armorInstanceIds,
            equipment,
            catalog);
        AppendCombatEquipmentList(
            builder,
            "방패",
            string.IsNullOrWhiteSpace(profile?.shieldInstanceId)
                ? Array.Empty<string>()
                : new[] { profile.shieldInstanceId },
            equipment,
            catalog);

        ICombatEquipmentMaintenanceRuntime maintenance = equipmentMaintenanceRuntime;
        EquipmentMaintenancePolicyData maintenancePolicy =
            maintenance?.GetPolicy(actor);
        builder.AppendLine();
        builder.AppendLine("장비 정비");
        builder.AppendLine(
            $"정책  {maintenancePolicy?.displayName ?? "표준"}"
            + (maintenancePolicy?.automaticRepair == true
                ? $" · {maintenancePolicy.sendAtDurability:P0}에 보내고 {maintenancePolicy.returnAtDurability:P0}에 복귀"
                : " · 자동 수리 꺼짐"));
        CombatEquipmentRepairOrder repairOrder = maintenance?.Orders
            .FirstOrDefault(order =>
                order != null
                && (string.Equals(
                        order.originalOwnerCharacterId,
                        characterId,
                        StringComparison.Ordinal)
                    || profile?.armorInstanceIds?.Contains(
                        order.equipmentInstanceId,
                        StringComparer.Ordinal) == true
                    || string.Equals(
                        profile?.shieldInstanceId,
                        order.equipmentInstanceId,
                        StringComparison.Ordinal)));
        builder.AppendLine(repairOrder != null
            ? $"수리 상태  {FormatRepairState(repairOrder.state)} · {repairOrder.ProgressRatio:P0}"
                + $" · 재료 {repairOrder.requiredGeneralMaterials} · 작업량 {repairOrder.completedWork:0.#}/{repairOrder.requiredWork:0.#}"
            : "수리 상태  대기 없음");

        CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor);
        int arrows = carry?.CountItem(CombatItemDefinitions.ArrowItemId) ?? 0;
        int bolts = carry?.CountItem(CombatItemDefinitions.BoltItemId) ?? 0;
        float carriedWeight = carry?.GetCurrentWeight() ?? 0f;
        float equippedWeight = equipment?.GetCarriedWeight(characterId) ?? 0f;
        builder.AppendLine($"탄약  화살 {arrows} · 볼트 {bolts}");
        builder.AppendLine(
            $"무게  {carriedWeight + equippedWeight:0.#}kg"
            + $" / 허용 {carry?.GetMaxAllowedWeight():0.#}kg");

        combatSummaryText.text = builder.ToString().TrimEnd();
        RefreshCombatCommandLabels(profile, weapon: equipment != null
            && equipment.TryGetActiveWeapon(characterId, out CombatWeaponSnapshot activeWeapon)
                ? activeWeapon
                : null);
    }

    private void RefreshCombatCommandLabels(
        CharacterCombatLoadoutProfile profile,
        CombatWeaponSnapshot weapon)
    {
        SetButtonLabel(
            combatLoadoutButton,
            string.Equals(profile?.profileId, CombatLoadoutPresetIds.Peace, StringComparison.Ordinal)
                ? "전투 장비"
                : "평시 장비");
        SetButtonLabel(combatWeaponButton, "무기 교체");
        SetButtonLabel(combatReloadButton, weapon?.RequiresAmmo == true
            ? $"재장전 {weapon.LoadedAmmo}/{weapon.MagazineCapacity}"
            : "재장전");
        SetButtonLabel(combatFireModeButton, FormatCombatFireMode(profile?.fireMode ?? CombatFireMode.Aimed));
        SetButtonLabel(combatHoldFireButton, profile?.holdFire == true ? "사격 중지" : "사격 허용");
        SetButtonLabel(combatRepairButton, "수리 요청");
        if (combatHoldFireButton != null)
        {
            DungeonUiTheme.StyleButton(combatHoldFireButton, selected: profile?.holdFire == true);
        }
    }

    private bool TryGetCombatRuntime(
        out ICombatEquipmentRuntime equipment,
        out string characterId)
    {
        equipment = combatEquipmentRuntime;
        characterId = actor?.Identity?.PersistentId ?? string.Empty;
        if (actor != null && equipment != null && !string.IsNullOrWhiteSpace(characterId))
        {
            return true;
        }

        gameEventBus.ShowNotice("전투 장비 정보를 불러올 수 없습니다.", NoticeFeedEvent.Grade.WARNING);
        return false;
    }

    private static void SetButtonLabel(Button button, string text)
    {
        TMP_Text label = button != null
            ? button.transform.Find("Label")?.GetComponent<TMP_Text>()
            : null;
        if (label != null)
        {
            label.text = text ?? string.Empty;
        }
    }

    private static void AppendCombatEquipmentList(
        StringBuilder builder,
        string label,
        IEnumerable<string> instanceIds,
        ICombatEquipmentRuntime equipment,
        ICombatEquipmentCatalog catalog)
    {
        string[] ids = instanceIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray()
            ?? Array.Empty<string>();
        if (ids.Length == 0 || equipment == null)
        {
            builder.AppendLine($"{label}  없음");
            return;
        }

        List<string> rows = new List<string>();
        foreach (string id in ids)
        {
            if (!equipment.TryGetInstance(id, out CombatEquipmentInstance instance)
                || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition))
            {
                continue;
            }

            rows.Add(
                $"{definition.DisplayName} [{CombatQualityRules.GetDisplayName(instance.quality)}]"
                + $" {instance.durabilityRatio * 100f:0}%");
        }

        builder.AppendLine(rows.Count > 0
            ? $"{label}  {string.Join(" · ", rows)}"
            : $"{label}  없음");
    }

    private static string FormatCombatFireMode(CombatFireMode mode)
    {
        return mode switch
        {
            CombatFireMode.Rapid => "속사",
            CombatFireMode.Suppressive => "제압",
            _ => "조준"
        };
    }

    private static string FormatRepairState(CombatEquipmentRepairOrderState state)
    {
        return state switch
        {
            CombatEquipmentRepairOrderState.PendingCombatEnd => "교전 종료 대기",
            CombatEquipmentRepairOrderState.WaitingForDelivery => "운반 대기",
            CombatEquipmentRepairOrderState.Ready => "수리 준비",
            CombatEquipmentRepairOrderState.InProgress => "수리 중",
            CombatEquipmentRepairOrderState.Completed => "완료",
            _ => "취소"
        };
    }

    public void RefreshHealthDetails()
    {
        if (healthSummaryText == null)
        {
            return;
        }

        if (actor == null)
        {
            healthSummaryText.text = "결핍 건강 정보가 없습니다.";
            RefreshCaptivityActionButton();
            return;
        }

        StringBuilder builder = new StringBuilder(512);
        if (deprivationRuntime.TryGetSnapshot(
                actor,
                out CharacterDeprivationSnapshot snapshot))
        {
            builder.AppendLine("결핍 부담");
            foreach (DeprivationKind kind in Enum.GetValues(typeof(DeprivationKind)))
            {
                float burden = snapshot.Burdens != null
                    && snapshot.Burdens.TryGetValue(kind, out float value)
                        ? value
                        : 0f;
                builder.AppendLine(
                    $"{FormatDeprivation(kind),-8} {burden,5:0.#}  {FormatBurdenState(burden)}");
            }

            builder.AppendLine();
            builder.AppendLine($"감염 부담  {snapshot.InfectionBurden:0.#} / 100");
            float highest = snapshot.HighestBurden;
            float mood01 = characterStats != null
                ? Mathf.Clamp01(characterStats.Mood / 100f)
                : 0.5f;
            float chance = highest >= 70f
                ? CharacterDeprivationRuntime.GetBreakdownChance(
                    actor,
                    highest,
                    mood01) * 100f
                : 0f;
            builder.AppendLine($"붕괴 확률  {chance:0.#}% / 5초");

            if (snapshot.Breakdown != null && snapshot.Breakdown.active)
            {
                builder.AppendLine(
                    $"현재 붕괴  {FormatBreakdown(snapshot.Breakdown.kind)}");
                builder.AppendLine(
                    $"원인  {FormatDeprivation(snapshot.Breakdown.cause)}");
                if (!string.IsNullOrWhiteSpace(snapshot.Breakdown.targetId))
                {
                    builder.AppendLine($"목표  {snapshot.Breakdown.targetId}");
                }

                builder.AppendLine(
                    $"제압 저항  {snapshot.Breakdown.suppressionResistance:0.#}");
            }

            builder.AppendLine();
            builder.AppendLine("최근 금기 행동");
            if (snapshot.TabooMemories == null
                || snapshot.TabooMemories.Count == 0)
            {
                builder.AppendLine("기록 없음");
            }
            else
            {
                foreach (string memory in snapshot.TabooMemories.TakeLast(5))
                {
                    builder.AppendLine($"- {memory}");
                }
            }
        }
        else
        {
            builder.AppendLine("결핍 건강 정보가 없습니다.");
        }

        AppendCaptivityDetails(builder);
        healthSummaryText.text = builder.ToString().TrimEnd();
        RefreshCaptivityActionButton();
    }

    private void AppendCaptivityDetails(StringBuilder builder)
    {
        string captiveId = actor?.Identity?.PersistentId ?? string.Empty;
        if (captivityRuntime == null
            || !captivityRuntime.TryGetCaptive(
                captiveId,
                out CaptiveState captive)
            || !captive.IsActive)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("포로 상태");
        builder.AppendLine(
            $"{FormatCaptivityStatus(captive.status)} · 건강 {captive.health:0}"
            + $" · 순응 {captive.compliance:0} · 탈출 위험 {captive.escapeRisk:0}");
        builder.AppendLine(
            $"의지 {captive.will:0} · 공포 {captive.fear:0}"
            + $" · 신뢰 {captive.trust:0} · 원한 {captive.grudge:0}"
            + $" · 타락 {captive.corruption:0}");
        if (captive.falseCompliance)
        {
            builder.AppendLine("복종 진위 불명: 원한이 높아 배신 가능성이 있습니다.");
        }

        if (!string.IsNullOrWhiteSpace(captive.lastResult))
        {
            builder.AppendLine($"최근 상태  {captive.lastResult}");
        }
    }

    private void RefreshCaptivityActionButton()
    {
        if (captivityActionButton == null)
        {
            return;
        }

        string captiveId = actor?.Identity?.PersistentId ?? string.Empty;
        CaptiveState captive = null;
        bool hasCaptive = captivityRuntime != null
            && captivityRuntime.TryGetCaptive(
                captiveId,
                out captive)
            && captive.IsActive;
        captivityActionButton.gameObject.SetActive(hasCaptive);
        if (!hasCaptive)
        {
            return;
        }

        string label = captive.status switch
        {
            CaptivityStatus.AwaitingCapture => "포획·호송 명령",
            CaptivityStatus.Confined => "기본 노역 허용",
            CaptivityStatus.Labor => "노역 중지",
            CaptivityStatus.Stabilizing
                or CaptivityStatus.AwaitingEscort
                or CaptivityStatus.Escorting => "포획 진행 중",
            _ => "운영 탭에서 관리"
        };
        TMP_Text text = captivityActionButton.transform
            .Find("Label")?.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.text = label;
        }

        captivityActionButton.interactable =
            captive.status is CaptivityStatus.AwaitingCapture
                or CaptivityStatus.Confined
                or CaptivityStatus.Labor;
    }

    private static string FormatDeprivation(DeprivationKind kind)
    {
        return kind switch
        {
            DeprivationKind.Hunger => "굶주림",
            DeprivationKind.Thirst => "탈수",
            DeprivationKind.Bladder => "방광 손상",
            DeprivationKind.Contamination => "오염",
            DeprivationKind.Exhaustion => "탈진",
            DeprivationKind.MentalInstability => "정신 불안",
            _ => kind.ToString()
        };
    }

    private static string FormatBurdenState(float burden)
    {
        if (burden >= 100f) return "붕괴 임박";
        if (burden >= 70f) return "위험";
        if (burden >= 40f) return "건강 이상";
        if (burden > 0.1f) return "누적 중";
        return "안정";
    }

    private static string FormatBreakdown(CharacterBreakdownKind kind)
    {
        return kind switch
        {
            CharacterBreakdownKind.DesperateRelief => "배변 통제 상실",
            CharacterBreakdownKind.DesperateDrink => "절박한 갈증",
            CharacterBreakdownKind.DesperateEat => "금기 포식",
            CharacterBreakdownKind.Collapse => "실신",
            CharacterBreakdownKind.ViolentImpulse => "폭력 충동",
            _ => "없음"
        };
    }

    public static string FormatMoodFactors(CharacterMoodSnapshot snapshot)
    {
        if (snapshot == null || snapshot.Factors == null || snapshot.Factors.Count == 0)
        {
            return "현재 기분을 바꾸는 요인이 없습니다.";
        }

        StringBuilder builder = new StringBuilder();
        AppendMoodFactorGroup(builder, snapshot.Factors, CharacterMoodFactorKind.Need, "욕구 영향");
        AppendMoodFactorGroup(builder, snapshot.Factors, CharacterMoodFactorKind.Interaction, "최근 경험");
        return builder.ToString().TrimEnd();
    }

    public static string FormatLogText(CharacterActor character, int maxLines = 8)
    {
        return FormatLogText(character != null ? character.LogComponent : null, maxLines);
    }

    public static string FormatLogText(CharacterLog characterLog, int maxLines = 8)
    {
        IReadOnlyList<string> entries = characterLog != null ? characterLog.Entries : null;
        if (entries == null || entries.Count == 0)
        {
            return "아직 기록이 없습니다.";
        }

        int start = Mathf.Max(0, entries.Count - Mathf.Max(1, maxLines));
        List<string> rows = new List<string>();
        for (int i = entries.Count - 1; i >= start; i--)
        {
            rows.Add($"- {entries[i]}");
        }

        return string.Join("\n\n", rows);
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

    private CharacterSkillDraft GetCurrentActiveDraft()
    {
        return progression?.Drafts
            .Where(draft => draft != null
                && draft.kind == CharacterSkillKind.Active
                && draft.isReady
                && !draft.permanentlyChosen)
            .OrderBy(draft => draft.unlockLevel)
            .FirstOrDefault();
    }

    private string BuildProgressionSummary(string traits)
    {
        StringBuilder builder = new StringBuilder(768);
        builder.AppendLine(
            $"Lv.{progression.Level}  ·  잠재력 {CharacterSkillDisplay.Potential(progression.PotentialGrade)}  ·  성장 +{progression.GrowthState.allocatedGrowthPoints}");
        builder.AppendLine($"특성  {(string.IsNullOrWhiteSpace(traits) ? "없음" : traits)}");
        builder.AppendLine("능력치  기본 | 종족·특성 | 레벨 | 장비 | 조건부 | 최종");

        ExpeditionEquipmentStatBlock equipment = GetCurrentEquipmentBonuses();
        foreach (CharacterStatDefinition definition in CharacterStatCatalog.All
                     .Where(item => item.LegacyType.HasValue))
        {
            CharacterStatType statType = definition.LegacyType.Value;
            CharacterStatBreakdown breakdown = progression.GetStatBreakdown(statType);
            int equipmentBonus = GetEquipmentBonus(equipment, statType);
            int finalValue = Mathf.Max(0, breakdown.FinalValue + equipmentBonus);
            builder.AppendLine(
                $"{definition.DisplayName}  {breakdown.BaseValue} | {FormatSigned(breakdown.SpeciesTraitValue)} | {FormatSigned(breakdown.LevelGrowthValue)} | {FormatSigned(equipmentBonus)} | {FormatSigned(breakdown.ConditionalPassiveValue)} | {finalValue}");
        }

        builder.AppendLine(equipment != null && equipment.maxHealth != 0
            ? $"장비 체력 {FormatSigned(equipment.maxHealth)} · 장비 보정은 출정 전투에만 적용"
            : "장비 보정은 출정 전투에만 적용");

        IReadOnlyList<CharacterGrowthAllocationRecord> records =
            progression.GrowthState.allocationRecords;
        if (records == null || records.Count == 0)
        {
            builder.Append("최근 성장  아직 레벨 성장 기록 없음");
        }
        else
        {
            builder.Append("최근 성장  ");
            builder.Append(string.Join(" / ", records
                .Where(record => record != null)
                .OrderByDescending(record => record.level)
                .Take(4)
                .Select(FormatGrowthAllocationRecord)));
        }

        return builder.ToString();
    }

    private string FormatGrowthAllocationRecord(CharacterGrowthAllocationRecord record)
    {
        string statName = CharacterStatCatalog.TryGet(record.statType, out CharacterStatDefinition definition)
            ? definition.DisplayName
            : record.statType.ToString();
        string reason = string.IsNullOrWhiteSpace(record.reason)
            ? "성장 기록"
            : record.reason;
        return $"Lv.{record.level} {statName}+1({reason})";
    }

    private void RefreshProfileAndVitals()
    {
        if (actor == null)
        {
            return;
        }

        if (profileText != null)
        {
            string species = !string.IsNullOrWhiteSpace(actor.SpeciesTag) ? actor.SpeciesTag : "종족 미상";
            int actorLevel = actor.Progression != null ? actor.Progression.Level : 1;
            profileText.text =
                $"Lv.{actorLevel} · {species} · {FormatRole(actor.Role)} · {FormatLifecycle(actor.CurrentLifecycleState)}";
        }

        if (characterStats == null)
        {
            SetMeter(health, 0f, "--");
            RefreshCarrySummary();
            return;
        }

        float maximum = Mathf.Max(1f, characterStats.MaxHealth);
        float current = Mathf.Clamp(characterStats.CurrentHealth, 0f, maximum);
        int injuryPercent = Mathf.RoundToInt(characterStats.InjurySeverity * 100f);
        SetMeter(
            health,
            current / maximum,
            injuryPercent > 0
                ? $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(maximum)} · 부상 {injuryPercent}%"
                : $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(maximum)}");
    }

    private void RefreshCarrySummary()
    {
        if (carrySummaryText == null)
        {
            return;
        }

        CharacterCarryInventory inventory = actor != null
            ? actor.GetComponent<CharacterCarryInventory>()
            : null;
        if (inventory == null)
        {
            carrySummaryText.text = "소지품 없음\n운반 한도 정보 없음";
            carrySummaryText.color = DungeonUiTheme.TextSecondary;
            return;
        }

        float currentWeight = inventory.GetCurrentWeight(itemCatalogProvider);
        float baseLimit = inventory.GetBaseCarryLimit();
        float maxAllowed = inventory.GetMaxAllowedWeight(haulingSettingsProvider);
        float speedMultiplier = inventory.GetMoveSpeedMultiplier(
            itemCatalogProvider,
            haulingSettingsProvider);
        bool overloaded = currentWeight > baseLimit + 0.01f;

        StringBuilder builder = new StringBuilder();
        AppendSurvivalStatus(builder);
        builder.AppendLine($"소지 무게 {FormatWeight(currentWeight)} / 기본 {FormatWeight(baseLimit)} / 최대 {FormatWeight(maxAllowed)}");
        builder.AppendLine(overloaded
            ? $"과적 중 · 이동 속도 {Mathf.RoundToInt(speedMultiplier * 100f)}%"
            : "과적 없음 · 이동 속도 100%");

        IReadOnlyList<CharacterCarriedItemSaveData> items = inventory.Items;
        List<string> entries = items == null
            ? new List<string>()
            : items
                .Where(item => item != null && item.quantity > 0)
                .GroupBy(item => item.itemId ?? string.Empty)
                .Select(group =>
                {
                    DungeonItemDefinition definition = itemCatalogProvider.GetDefinition(group.Key);
                    return $"{definition.DisplayName} x{group.Sum(item => item.quantity)}";
                })
                .Take(4)
                .ToList();
        builder.Append(entries.Count > 0 ? string.Join(" · ", entries) : "소지 아이템 없음");

        carrySummaryText.text = builder.ToString();
        carrySummaryText.color = overloaded ? DungeonUiTheme.Warning : DungeonUiTheme.TextSecondary;
    }

    private void AppendSurvivalStatus(StringBuilder builder)
    {
        if (builder == null
            || actor == null
            || !survivalFoodRuntime.TryGetCharacterStatus(actor, out SurvivalCharacterStatus status))
        {
            return;
        }

        string healthLabel = FormatSurvivalHealthState(status.PrimaryState);
        string temperature = status.TemperatureComfort01 >= 0.75f
            ? "체온 안정"
            : status.TemperatureComfort01 >= 0.45f
                ? "체온 주의"
                : "체온 위험";
        string issueSuffix = status.ActiveIssueCount > 1
            ? $" 외 {status.ActiveIssueCount - 1}건"
            : string.Empty;
        builder.AppendLine(
            $"생존 상태 {healthLabel}{issueSuffix} · {status.FoodSummary} · {status.WaterSummary} · {temperature}");
    }

    private void UnbindCharacter()
    {
        if (actor == null && characterStats == null && characterLog == null)
        {
            return;
        }

        if (characterStats != null)
        {
            characterStats.OnStatChange -= OnStatChange;
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
        pendingCandidateConfirmation = -1;
        pendingCandidateUnlockLevel = -1;
        nextVitalsRefreshAt = 0f;
    }

    private ExpeditionEquipmentStatBlock GetCurrentEquipmentBonuses()
    {
        string characterId = actor?.Identity?.PersistentId;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return ExpeditionEquipmentStatBlock.Empty;
        }

        IExpeditionEquipmentRuntime runtime = ResolveEquipmentRuntime();
        return runtime?.GetCombatBonuses(characterId) ?? ExpeditionEquipmentStatBlock.Empty;
    }

    private IExpeditionEquipmentRuntime ResolveEquipmentRuntime()
    {
        return equipmentRuntime;
    }

    private IUiPopupService ResolvePopupService()
    {
        if (popupService == null)
        {
            throw new InvalidOperationException(
                $"{nameof(CharacterSummeryInfo)} requires VContainer injection of {nameof(IUiPopupService)}.");
        }

        return popupService;
    }

    private ICharacterSummaryRuntimeLogFactory ResolveRuntimeLogFactory()
    {
        if (runtimeLogFactory == null)
        {
            throw new InvalidOperationException(
                $"{nameof(CharacterSummeryInfo)} requires VContainer injection of {nameof(ICharacterSummaryRuntimeLogFactory)}.");
        }

        return runtimeLogFactory;
    }

    private GameObject RequireUiRoot()
    {
        if (UI == null)
        {
            throw new InvalidOperationException($"{nameof(CharacterSummeryInfo)} requires a UI root reference.");
        }

        return UI;
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

    private static void AppendMoodFactorGroup(
        StringBuilder builder,
        IReadOnlyList<CharacterMoodFactorSnapshot> factors,
        CharacterMoodFactorKind kind,
        string heading)
    {
        bool hasGroup = factors.Any(factor => factor != null && factor.Kind == kind);
        if (!hasGroup)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.AppendLine(heading);
        foreach (CharacterMoodFactorSnapshot factor in factors)
        {
            if (factor == null || factor.Kind != kind)
            {
                continue;
            }

            string signedValue = factor.Value >= 0f
                ? $"+{Mathf.RoundToInt(factor.Value)}"
                : $"{Mathf.RoundToInt(factor.Value)}";
            builder.AppendLine(kind == CharacterMoodFactorKind.Interaction
                ? $"- {factor.Label}  {signedValue}  · {FormatRemainingTime(factor.RemainingSeconds)}"
                : $"- {factor.Label}  {signedValue}");
        }
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

    private static int GetEquipmentBonus(
        ExpeditionEquipmentStatBlock equipment,
        CharacterStatType statType)
    {
        if (equipment == null)
        {
            return 0;
        }

        return statType switch
        {
            CharacterStatType.Attack => equipment.attack,
            CharacterStatType.Strength => equipment.strength,
            CharacterStatType.Toughness => equipment.toughness,
            CharacterStatType.Dexterity => equipment.dexterity,
            CharacterStatType.MoveSpeed => equipment.moveSpeed,
            _ => 0
        };
    }

    private static string FormatSurvivalHealthState(SurvivalHealthState state)
    {
        return state switch
        {
            SurvivalHealthState.Thirsty => "갈증",
            SurvivalHealthState.Hungry => "배고픔",
            SurvivalHealthState.Exposed => "노출",
            SurvivalHealthState.Sick => "질병",
            SurvivalHealthState.Infected => "감염",
            SurvivalHealthState.Recovering => "회복 중",
            _ => "건강"
        };
    }

    private static string FormatRemainingTime(float remainingSeconds)
    {
        int seconds = Mathf.Max(1, Mathf.CeilToInt(remainingSeconds));
        if (seconds < 60)
        {
            return $"{seconds}초";
        }

        int minutes = seconds / 60;
        int remainder = seconds % 60;
        return remainder > 0 ? $"{minutes}분 {remainder}초" : $"{minutes}분";
    }

    private static string FormatRole(CharacterRole role)
    {
        return role == CharacterRole.Owner ? "사장" : "일반";
    }

    private static string FormatLifecycle(CharacterLifecycleState state)
    {
        return state switch
        {
            CharacterLifecycleState.SpawningOutside => "입장 준비",
            CharacterLifecycleState.EnteringDungeon => "입장 중",
            CharacterLifecycleState.Active => "활동 중",
            CharacterLifecycleState.ExitingDungeon => "퇴장 중",
            CharacterLifecycleState.OnExpedition => "원정 중",
            CharacterLifecycleState.PreparingExpedition => "출정 준비",
            CharacterLifecycleState.DepartingExpedition => "출정 중",
            CharacterLifecycleState.ReturningExpedition => "귀환 중",
            CharacterLifecycleState.Downed => "쓰러짐",
            CharacterLifecycleState.Despawned => "퇴장",
            _ => "대기"
        };
    }

    private static string FormatCaptivityStatus(CaptivityStatus status)
    {
        return status switch
        {
            CaptivityStatus.AwaitingCapture => "포획 대기",
            CaptivityStatus.Stabilizing => "현장 안정화",
            CaptivityStatus.AwaitingEscort => "호송 대기",
            CaptivityStatus.Escorting => "호송 중",
            CaptivityStatus.Confined => "수용 중",
            CaptivityStatus.Labor => "노역 중",
            CaptivityStatus.Interaction => "관리 작업 중",
            CaptivityStatus.Performer => "공연 참가",
            CaptivityStatus.EscapeAttempt => "탈출 시도",
            CaptivityStatus.Ransom => "몸값 협상",
            _ => status.ToString()
        };
    }

    private static bool IsAvailableCaptureCarrier(
        CharacterActor candidate,
        string captiveId)
    {
        return candidate != null
            && !candidate.IsDead
            && candidate.CurrentLifecycleState == CharacterLifecycleState.Active
            && candidate.characterType == CharacterType.NPC
            && !string.Equals(
                candidate.Identity?.PersistentId,
                captiveId,
                StringComparison.Ordinal)
            && candidate.TryGetAbility(out AbilityMove _);
    }

    private void ShowCaptivityNotice(string message, bool success)
    {
        gameEventBus.ShowNotice(
            string.IsNullOrWhiteSpace(message)
                ? "포로 명령을 처리하지 못했습니다."
                : message,
            success
                ? NoticeFeedEvent.Grade.NONE
                : NoticeFeedEvent.Grade.WARNING);
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }

    private static string FormatWeight(float weight)
    {
        return $"{Mathf.Max(0f, weight):0.#}kg";
    }

    private static string FormatSigned(int value)
    {
        return value > 0 ? "+" + value : value.ToString();
    }

    private static string Fallback(string text, string fallback)
    {
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
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
