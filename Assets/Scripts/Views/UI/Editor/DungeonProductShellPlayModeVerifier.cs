using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class DungeonProductShellPlayModeVerifier
{
    public const string RequestPath = "Temp/product-shell-verification.request";
    public const string ReportPath = "Temp/product-shell-verification-report.txt";
    public const string TitleCapturePath = "Temp/product-shell-title.png";
    public const string SettingsCapturePath = "Temp/product-shell-settings.png";
    public const string DifficultyCapturePath = "Temp/product-shell-difficulty.png";
    public const string BattleCapturePath = "Temp/offense-turn-battle.png";
    public const string BattleChangedCapturePath = "Temp/offense-turn-battle-changed.png";

    private const string BackupMarkerPath = "Temp/product-shell-verification.backup";
    private const string BackupDirectory = "Temp/product-shell-backup";
    private static readonly string[] RunSlots = { "autosave", "quicksave", "manual" };
    private static bool runnerCreated;

    static DungeonProductShellPlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Request Product Shell Verification")]
    public static void RequestRunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        RestoreBackups();
        PrepareCleanProfile();
        Directory.CreateDirectory("Temp");
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    private static void OnEditorUpdate()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode
            && File.Exists(BackupMarkerPath)
            && !File.Exists(RequestPath))
        {
            RestoreBackups();
        }

        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(BackupMarkerPath))
        {
            RestoreBackups();
            PrepareCleanProfile();
        }

        if (!string.Equals(SceneManager.GetActiveScene().path, "Assets/Scenes/TitleScene.unity", StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity", OpenSceneMode.Single);
        }

        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            RestoreBackups();
            PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
            return;
        }

        if (change != PlayModeStateChange.EnteredPlayMode || runnerCreated || !File.Exists(RequestPath))
        {
            return;
        }

        DungeonProductShellVerificationRunner existingRunner =
            UnityEngine.Object.FindFirstObjectByType<DungeonProductShellVerificationRunner>();
        if (existingRunner != null)
        {
            Debug.Log("Product Shell verification runner already exists at PlayMode entry.");
            runnerCreated = true;
            return;
        }

        runnerCreated = true;
        Debug.Log("Product Shell verification runner created at PlayMode entry.");
        GameObject runner = new GameObject("Product Shell Verification Runner");
        UnityEngine.Object.DontDestroyOnLoad(runner);
        runner.AddComponent<DungeonProductShellVerificationRunner>();
    }

    private static void PrepareCleanProfile()
    {
        Directory.CreateDirectory(BackupDirectory);
        foreach (string backup in Directory.GetFiles(BackupDirectory))
        {
            File.Delete(backup);
        }

        foreach (string slot in RunSlots)
        {
            BackupAndDelete(GetSavePath(slot), Path.Combine(BackupDirectory, slot + ".json"));
        }

        BackupAndDelete(GetSettingsPath(), Path.Combine(BackupDirectory, "user-settings.json"));
        BackupAndDelete(GetMigrationMarkerPath(), Path.Combine(BackupDirectory, "legacy-migration.done"));
        Directory.CreateDirectory(Path.GetDirectoryName(GetMigrationMarkerPath()) ?? Application.persistentDataPath);
        File.WriteAllText(GetMigrationMarkerPath(), "product-shell verification");
        File.WriteAllText(BackupMarkerPath, DateTime.UtcNow.ToString("O"));
    }

    private static void RestoreBackups()
    {
        if (!File.Exists(BackupMarkerPath))
        {
            return;
        }

        foreach (string slot in RunSlots)
        {
            RestoreFile(Path.Combine(BackupDirectory, slot + ".json"), GetSavePath(slot));
        }

        RestoreFile(Path.Combine(BackupDirectory, "user-settings.json"), GetSettingsPath());
        RestoreFile(Path.Combine(BackupDirectory, "legacy-migration.done"), GetMigrationMarkerPath());
        File.Delete(BackupMarkerPath);
    }

    private static void BackupAndDelete(string source, string backup)
    {
        if (!File.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(backup) ?? BackupDirectory);
        File.Copy(source, backup, true);
        File.Delete(source);
    }

    private static void RestoreFile(string backup, string target)
    {
        if (File.Exists(target))
        {
            File.Delete(target);
        }

        if (!File.Exists(backup))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? string.Empty);
        File.Copy(backup, target, true);
        File.Delete(backup);
    }

    private static string GetSavePath(string slot)
    {
        return Path.Combine(Application.persistentDataPath, "Saves", slot + ".json");
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(Application.persistentDataPath, "Settings", "user-settings.json");
    }

    private static string GetMigrationMarkerPath()
    {
        return Path.Combine(
            Application.persistentDataPath,
            "Migration",
            "legacy-default-company-v1.done");
    }
}

public sealed class DungeonProductShellVerificationRunner : MonoBehaviour
{
    private readonly List<string> report = new List<string>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private InputSettings.EditorInputBehaviorInPlayMode originalInputBehavior;
    private Mouse originalMouse;
    private Mouse verificationMouse;
    private int verificationMouseSerial;
    private CharacterActor preparedRecruitCandidate;
    private RegularCustomerRuntime preparedRecruitment;
    private string preparedRecruitDiagnostic = string.Empty;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        Debug.Log("Product Shell verification runner started.");
        yield return Run();
    }

    private IEnumerator Run()
    {
        Directory.CreateDirectory("Temp");
        Application.logMessageReceived += CaptureLog;
        originalInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        originalMouse = Mouse.current;
        if (originalMouse != null)
        {
            InputSystem.DisableDevice(originalMouse);
        }

        CreateVerificationMouse();

        try
        {
            Debug.Log("Product Shell verification runner executing.");
            yield return new WaitForSecondsRealtime(2f);
            DungeonTitleLifetimeScope titleScope = FindTitleScope();
            Check(titleScope != null, "TITLE_DI_SCOPE", "dedicated title LifetimeScope resolved");
            Check(FindScope() == null, "TITLE_ISOLATION", "title creates no Gameplay LifetimeScope");
            if (titleScope == null)
            {
                yield break;
            }

            IDungeonSaveSlotCatalog slotCatalog = titleScope.Container.Resolve<IDungeonSaveSlotCatalog>();
            IDungeonUserSettingsService settings = titleScope.Container.Resolve<IDungeonUserSettingsService>();
            IDungeonAudioService audio = titleScope.Container.Resolve<IDungeonAudioService>();
            IDungeonSceneNavigator navigator = titleScope.Container.Resolve<IDungeonSceneNavigator>();
            GameObject titleRoot = FindSceneObject("DungeonTitleRuntimeUI");
            Button continueButton = FindSceneComponent<Button>("ContinueLatestButton");
            Button newGameButton = FindSceneComponent<Button>("StartNewRunButton");
            Image brandIcon = FindSceneComponent<Image>("BrandIcon");

            Check(SceneManager.GetActiveScene().name == DungeonSceneNavigator.TitleSceneName
                    && titleRoot != null && titleRoot.activeInHierarchy,
                "FRESH_TITLE", "dedicated title scene is the active first screen");
            Check(FindTexts("DungeonStory").Any(text => text.gameObject.activeInHierarchy),
                "TITLE_BRAND", "DungeonStory is a first-viewport title");
            Check(brandIcon != null && brandIcon.gameObject.activeInHierarchy && brandIcon.sprite != null,
                "TITLE_BRAND_ICON", brandIcon != null && brandIcon.sprite != null
                    ? $"sprite={brandIcon.sprite.name}; size={brandIcon.rectTransform.rect.size}"
                    : "brand icon or sprite missing");
            Check(continueButton != null && !continueButton.interactable,
                "CONTINUE_EMPTY", "Continue is disabled without a valid save");
            Check(Mathf.Approximately(Time.timeScale, 1f),
                "TITLE_TIME", $"title owns no paused Gameplay clock; timeScale={Time.timeScale:0.##}");
            Check(IsUiRaycastBlocking(titleRoot), "TITLE_INPUT_BLOCK", "title UI owns pointer input");
            Check(!settings.Current.highContrast, "TITLE_STANDARD_THEME",
                "fresh profile starts in the brighter standard UI theme");
            CheckStandardThemeLuminance("TITLE_STANDARD_THEME_LUMA");
            Check(audio.IsReady && FindSceneObject("DungeonAudioRuntime") != null,
                "AUDIO_READY", "audio runtime is available on the title");
            Check(CountPlayingLoops() == 2, "AUDIO_LOOPS", $"music and ambience loops={CountPlayingLoops()}");
            DungeonAudioLibrarySO authoredLibrary = Resources.Load<DungeonAudioLibrarySO>("Audio/DungeonAudioLibrary");
            DungeonAudioCue[] authoredCues = Enum.GetValues(typeof(DungeonAudioCue))
                .Cast<DungeonAudioCue>()
                .ToArray();
            Check(authoredLibrary != null && authoredCues.All(cue => authoredLibrary.GetCue(cue) != null),
                "AUDIO_AUTHORED_LIBRARY",
                authoredLibrary != null
                    ? $"authored cues={authoredCues.Count(cue => authoredLibrary.GetCue(cue) != null)}/{authoredCues.Length}"
                    : "authored library missing");
            int authoredCueCountBefore = audio.PlayedCueCount;
            foreach (DungeonAudioCue cue in authoredCues)
            {
                audio.Play(cue);
            }

            Check(audio.PlayedCueCount == authoredCueCountBefore + authoredCues.Length
                    && audio.LastCue == DungeonAudioCue.Defeat,
                "AUDIO_CUE_ROUTING",
                $"all authored cue routes played; delta={audio.PlayedCueCount - authoredCueCountBefore}");

            yield return Capture(DungeonProductShellPlayModeVerifier.TitleCapturePath, "TITLE_CAPTURE");

            int cueCountBefore = audio.PlayedCueCount;
            yield return Click(FindSceneComponent<Button>("StartupSettingsButton"));
            GameObject settingsModal = FindSceneObject("SettingsModal");
            Check(settingsModal != null && settingsModal.activeInHierarchy,
                "SETTINGS_OPEN", "title Settings opens through pointer input");
            Check(titleRoot != null && titleRoot.activeInHierarchy,
                "SETTINGS_OVER_TITLE", "title remains visible beneath Settings");
            Check(audio.PlayedCueCount > cueCountBefore && audio.LastCue == DungeonAudioCue.UiClick,
                "UI_AUDIO", $"pointer click produced UI cue count={audio.PlayedCueCount}");
            Check(IsPanelInsideScreen("SettingsPanel"), "SETTINGS_BOUNDS",
                $"settings panel fits {Screen.width}x{Screen.height}");
            yield return Capture(DungeonProductShellPlayModeVerifier.SettingsCapturePath, "SETTINGS_CAPTURE");

            yield return Click(FindSceneComponent<Button>("AudioSettingsTab"));
            Check(IsActive("SettingsPage_1"), "AUDIO_TAB", "audio settings tab changes visible page");
            settings.Update(data =>
            {
                data.masterVolume = 0.42f;
                data.musicVolume = 0.36f;
                data.effectsVolume = 0.58f;
                data.uiVolume = 0.74f;
            });
            yield return null;
            GameObject audioRoot = FindSceneObject("DungeonAudioRuntime");
            AudioSource musicSource = FindAudioSource(audioRoot, "Music");
            AudioSource ambienceSource = FindAudioSource(audioRoot, "Ambience");
            AudioSource effectsSource = FindAudioSource(audioRoot, "Effects");
            AudioSource uiSource = FindAudioSource(audioRoot, "UI");
            Check(Mathf.Abs(AudioListener.volume - 0.42f) < 0.001f,
                "AUDIO_MASTER_VOLUME", $"listener={AudioListener.volume:0.###}");
            bool channelsMatch = musicSource != null
                && ambienceSource != null
                && effectsSource != null
                && uiSource != null
                && Mathf.Abs(musicSource.volume - 0.16f * 0.36f) < 0.001f
                && Mathf.Abs(ambienceSource.volume - 0.09f * 0.36f) < 0.001f
                && Mathf.Abs(effectsSource.volume - 0.58f) < 0.001f
                && Mathf.Abs(uiSource.volume - 0.7f * 0.74f) < 0.001f;
            Check(channelsMatch, "AUDIO_CHANNEL_VOLUMES",
                $"music={musicSource?.volume:0.###}; ambience={ambienceSource?.volume:0.###}; "
                + $"effects={effectsSource?.volume:0.###}; ui={uiSource?.volume:0.###}");
            yield return Click(FindSceneComponent<Button>("AccessibilitySettingsTab"));
            Check(IsActive("SettingsPage_2"), "ACCESSIBILITY_TAB", "accessibility tab changes visible page");
            Toggle contrastToggle = FindSceneComponent<Toggle>("HighContrastToggle");
            yield return Click(contrastToggle);
            Check(settings.Current.highContrast,
                "HIGH_CONTRAST", "high contrast changes authoritative persisted settings");
            CheckHighContrastThemeLuminance("HIGH_CONTRAST_THEME_LUMA");
            Check(File.Exists(settings.SettingsPath), "SETTINGS_SAVED", "settings file is written immediately");

            yield return Click(FindSceneComponent<Button>("SettingsCloseButton"));
            Check(settingsModal != null && !settingsModal.activeInHierarchy,
                "SETTINGS_CLOSE", "Settings closes through pointer input");
            Check(titleRoot != null && titleRoot.activeInHierarchy && Mathf.Approximately(Time.timeScale, 1f),
                "TITLE_RESTORED", "closing Settings restores the standalone title");

            yield return Click(newGameButton);
            GameObject difficultyModal = FindSceneObject("DifficultyModal");
            Check(difficultyModal != null && difficultyModal.activeInHierarchy,
                "DIFFICULTY_MODAL", "New Game opens difficulty selection through pointer input");
            yield return Capture(DungeonProductShellPlayModeVerifier.DifficultyCapturePath, "DIFFICULTY_CAPTURE");
            yield return Click(FindSceneComponent<Button>("DifficultyHardButton"));
            yield return Click(FindSceneComponent<Button>("DifficultyNextButton"));
            Check(!navigator.StartNewGame(DungeonDifficulty.Normal),
                "DUPLICATE_TRANSITION", "duplicate scene request is rejected after difficulty selection");
            yield return WaitForCondition(
                () => SceneManager.GetActiveScene().name == DungeonSceneNavigator.PreparationSceneName
                    && FindSceneComponent<Button>("PreparationOwnerNextButton") != null,
                10f);
            Button preparationOwner = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(candidate => candidate != null
                    && candidate.gameObject.scene.IsValid()
                    && candidate.gameObject.activeInHierarchy
                    && candidate.interactable
                    && candidate.name.StartsWith("OwnerCandidate_", StringComparison.Ordinal));
            Check(preparationOwner != null,
                "PREPARATION_OWNER",
                "StartPreparationScene exposes owner choices after difficulty selection");
            yield return Click(preparationOwner);
            yield return Click(FindSceneComponent<Button>("PreparationOwnerNextButton"));
            yield return WaitForCondition(
                () => FindSceneComponent<Button>("PreparationStartRunButton")?.interactable == true,
                10f);
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            yield return WaitForCondition(
                () => SceneManager.GetActiveScene().name == DungeonSceneNavigator.GameplaySceneName && FindScope() != null,
                10f);
            yield return new WaitForSecondsRealtime(0.5f);

            DungeonRuntimeLifetimeScope scope = FindScope();
            Check(scope != null && SceneManager.GetActiveScene().name == DungeonSceneNavigator.GameplaySceneName,
                "NEW_GAME", "New Game loads the dedicated Gameplay scene");
            Check(FindTitleScope() == null, "GAMEPLAY_ISOLATION", "Gameplay unloads the title LifetimeScope");
            if (scope == null)
            {
                yield break;
            }

            IDungeonGameSaveSlotService slots = scope.Container.Resolve<IDungeonGameSaveSlotService>();
            Check(!slots.HasSave(DungeonGameSaveSlotService.AutoSaveSlot)
                && !slots.HasSave(DungeonGameSaveSlotService.QuickSaveSlot)
                && !slots.HasSave(DungeonGameSaveSlotService.ManualSaveSlot),
                "NEW_GAME_CLEAN", "new run begins without stale run slots");
            Check(FindSceneComponent<Button>("StartNewRunButton") == null,
                "NO_GAMEPLAY_TITLE_ACTIONS", "Gameplay does not recreate title buttons");

            OwnerRunManager ownerManager = FindFirstObjectByType<OwnerRunManager>(FindObjectsInactive.Include);
            Button ownerButton = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(candidate => candidate != null
                    && candidate.gameObject.scene.IsValid()
                    && candidate.gameObject.activeInHierarchy
                    && candidate.name.StartsWith("OwnerOption_", StringComparison.Ordinal));
            Check(ownerButton == null,
                "NO_GAMEPLAY_OWNER_SELECTION",
                "prepared runs do not show the legacy gameplay owner selection");
            Check(ownerManager != null && ownerManager.CurrentOwnerActor != null,
                "OWNER_SELECTED", "prepared owner is spawned when gameplay loads");
            Check(Time.timeScale > 0f, "RUN_RESUMED", $"clock resumes after prepared start; timeScale={Time.timeScale:0.##}");

            yield return new WaitForSecondsRealtime(1f);
            RunVariableRuntime runVariableRuntime = scope.Container
                .Resolve<DungeonSceneRuntimeReferences>()
                .RunVariables;
            Check(runVariableRuntime != null
                    && runVariableRuntime.State.StartVariables?.runDifficulty == DungeonDifficulty.Hard,
                "FIXED_RUN_DIFFICULTY",
                $"selected={runVariableRuntime?.State.StartVariables?.runDifficulty}");

            yield return PrepareRecruitCandidate(scope);
            CharacterActor recruitCandidate = preparedRecruitCandidate;
            RegularCustomerRuntime recruitment = preparedRecruitment;
            string recruitCandidateId = RegularCustomerService.GetCustomerId(recruitCandidate);
            RegularCustomerRecord recruitRecord = null;
            bool recruitCandidateReady = recruitment != null
                && recruitment.State.TryGetRecord(recruitCandidateId, out recruitRecord)
                && recruitRecord.IsRecruitCandidate;
            Check(recruitCandidateReady,
                "RECRUIT_CANDIDATE_READY",
                $"candidate={recruitCandidateId}; status={recruitRecord?.Status}; {preparedRecruitDiagnostic}");

            Button operationsTab = FindTabButton(TabId.Operations);
            yield return Click(operationsTab);
            yield return WaitForCondition(
                () => FindSceneComponent<Button>($"P0Action_Recruit_{recruitCandidateId}") != null,
                4f);
            Button recruitButton = FindSceneComponent<Button>($"P0Action_Recruit_{recruitCandidateId}");
            yield return ScrollIntoView(recruitButton);
            Check(
                IsTopUiTarget(recruitButton, out string recruitRaycastDiagnostic),
                "RECRUIT_BUTTON_RAYCAST",
                recruitRaycastDiagnostic);
            yield return Click(recruitButton);
            yield return WaitForCondition(
                () => recruitment != null
                    && recruitment.State.IsRecruited(recruitCandidateId),
                2f);
            bool eligibleAfterRecruitment = recruitCandidate != null
                && recruitCandidate.Identity != null
                && recruitCandidate.Identity.CharacterType == CharacterType.NPC
                && recruitCandidate.TryGetAbility(out AbilityWork _)
                && OffenseExpeditionService.CanJoinExpedition(recruitCandidate, out _);
            GameObject recruitFeedback = FindSceneObject("P0Feedback");
            string recruitFeedbackText = recruitFeedback != null
                ? recruitFeedback.GetComponentInChildren<TMP_Text>(true)?.text ?? string.Empty
                : "<missing>";
            Check(recruitment != null
                    && recruitment.State.IsRecruited(recruitCandidateId)
                    && eligibleAfterRecruitment,
                "RECRUIT_TO_EXPEDITION_POINTER",
                $"recruited={recruitment?.State.IsRecruited(recruitCandidateId)}; type={recruitCandidate?.Identity?.CharacterType}; eligible={eligibleAfterRecruitment}; feedback={recruitFeedbackText}");

            Button expeditionTab = FindTabButton(TabId.Expedition);
            yield return Click(expeditionTab);
            yield return WaitForCondition(
                () => FindSceneComponent<Button>("P1Action_OffenseOpenMap") != null,
                4f);
            yield return Click(FindSceneComponent<Button>("P1Action_OffenseOpenMap"));
            yield return WaitForCondition(
                () => IsActive("OffenseWorldMapPanel")
                    && FindSceneComponent<Button>("Button_외곽 식재료 농장") != null,
                4f);
            yield return Click(FindSceneComponent<Button>("Button_외곽 식재료 농장"));
            OffenseWorldMapRuntime offenseMap = FindFirstObjectByType<OffenseWorldMapRuntime>(FindObjectsInactive.Include);
            Check(offenseMap != null
                    && string.Equals(offenseMap.State.SelectedTargetId, "food_farm", StringComparison.Ordinal),
                "OFFENSE_TARGET_POINTER",
                $"selected={offenseMap?.State.SelectedTargetId}");
            yield return Click(FindSceneComponent<Button>("Button_닫기"));
            yield return Click(FindSceneComponent<Button>("P1Action_OffenseOpenExpedition"));
            yield return WaitForCondition(
                () => IsActive("OffenseExpeditionPanel")
                    && FindOffenseExpeditionMemberButton() != null,
                4f);
            yield return Click(FindOffenseExpeditionMemberButton());
            yield return Click(FindSceneComponent<Button>("Button_원정 출발"));

            IOffenseBattleRuntime battle = scope.Container.Resolve<IOffenseBattleRuntime>();
            OffenseExpeditionRuntime activeExpedition = scope.Container
                .Resolve<OffenseSceneRuntimeReferences>()
                .Expedition;
            Check(activeExpedition?.ActiveExpeditions.FirstOrDefault()?.Phase == OffenseExpeditionPhase.ChoosingRoute,
                "JOURNEY_STARTED",
                $"phase={activeExpedition?.ActiveExpeditions.FirstOrDefault()?.Phase}");
            yield return AdvanceJourneyToFirstBattle(activeExpedition, battle);
            Check(battle.HasActiveBattle
                    && battle.IsBattleViewVisible
                    && FindFirstObjectByType<OffenseBattlePanel>(FindObjectsInactive.Include) != null,
                "BATTLE_STARTED", $"battle={battle.Session?.BattleId}; target={battle.Session?.TargetId}");
            float simulationTimeBefore = Time.time;
            yield return new WaitForSecondsRealtime(0.75f);
            Check(Time.time > simulationTimeBefore + 0.25f,
                "BATTLE_DUNGEON_CONTINUES",
                $"scaled time={simulationTimeBefore:0.###}->{Time.time:0.###}");
            yield return Capture(DungeonProductShellPlayModeVerifier.BattleCapturePath, "BATTLE_CAPTURE");

            long guardCommandBefore = battle.Session?.LastProcessedCommandId ?? -1;
            yield return Click(FindSceneComponent<Button>("Button_방어"));
            Check(battle.HasActiveBattle && battle.Session.LastProcessedCommandId > guardCommandBefore,
                "BATTLE_GUARD_POINTER",
                $"command={guardCommandBefore}->{battle.Session?.LastProcessedCommandId}");
            yield return WaitForCondition(
                () => battle.Session?.CurrentActor?.Team == OffenseBattleTeam.Allies,
                4f);
            yield return Click(FindSceneComponent<Button>("Button_던전 보기"));
            Check(!battle.IsBattleViewVisible && IsActive("Button_전투 복귀"),
                "BATTLE_DUNGEON_SWITCH", "Dungeon view hides battle content and exposes return command");
            yield return Click(FindSceneComponent<Button>("SaveMenuButton"));
            yield return Click(FindSceneComponent<Button>("SaveButton_manual"));
            string expectedBattleState = JsonUtility.ToJson(battle.CapturePersistentState());
            OffenseExpeditionRuntime expeditionRuntime = scope.Container
                .Resolve<OffenseSceneRuntimeReferences>()
                .Expedition;
            int historyBeforeRestore = expeditionRuntime != null
                ? expeditionRuntime.ResultHistory.Count
                : -1;
            yield return Click(FindSceneComponent<Button>("CloseButton", "SavePanel"));
            yield return Click(FindSceneComponent<Button>("Button_전투 복귀"));

            OffenseBattleCombatant enemy = battle.Session?.Combatants
                .FirstOrDefault(combatant => combatant.Team == OffenseBattleTeam.Enemies && !combatant.IsDead);
            float enemyHealthBefore = enemy?.CurrentHealth ?? -1f;
            long attackCommandBefore = battle.Session?.LastProcessedCommandId ?? -1;
            yield return Click(FindSceneComponent<Button>("Button_공격"));
            yield return Click(enemy != null
                ? FindSceneComponent<Button>($"Combatant_{enemy.PersistentId}")
                : null);
            yield return WaitForCondition(
                () => FindSceneComponent<Button>("Button_공격 확정") != null,
                2f);
            yield return Click(FindSceneComponent<Button>("Button_공격 확정"));
            Check(enemy != null
                    && battle.Session.LastProcessedCommandId > attackCommandBefore,
                "BATTLE_ATTACK_TARGET_POINTER",
                $"enemy={enemy?.PersistentId}; hp={enemyHealthBefore:0.##}->{enemy?.CurrentHealth:0.##}; command={attackCommandBefore}->{battle.Session?.LastProcessedCommandId}");
            yield return Capture(DungeonProductShellPlayModeVerifier.BattleChangedCapturePath, "BATTLE_CHANGED_CAPTURE");

            yield return Click(FindSceneComponent<Button>("Button_던전 보기"));
            yield return Click(FindSceneComponent<Button>("SaveMenuButton"));
            yield return Click(FindSceneComponent<Button>("LoadButton_manual"));
            yield return null;
            yield return null;
            string restoredBattleState = JsonUtility.ToJson(battle.CapturePersistentState());
            int historyAfterRestore = expeditionRuntime != null ? expeditionRuntime.ResultHistory.Count : -1;
            Check(battle.HasActiveBattle
                    && battle.IsBattleViewVisible
                    && string.Equals(restoredBattleState, expectedBattleState, StringComparison.Ordinal)
                    && historyAfterRestore == historyBeforeRestore,
                "BATTLE_EXACT_SAVE_RESTORE",
                $"stateEqual={restoredBattleState == expectedBattleState}; history={historyBeforeRestore}->{historyAfterRestore}; actor={battle.Session?.CurrentActor?.PersistentId}; difference={DescribeFirstDifference(expectedBattleState, restoredBattleState)}");

            yield return Click(FindSceneComponent<Button>("Button_던전 보기"));
            yield return Click(FindSceneComponent<Button>("SettingsMenuButton"));
            settingsModal = FindSceneObject("SettingsModal");
            Check(settingsModal != null && settingsModal.activeInHierarchy,
                "INGAME_SETTINGS", "upper-right Option opens the same Settings surface");
            yield return Click(FindSceneComponent<Button>("ApplySettingsButton"));
            Check(Time.timeScale > 0f, "INGAME_SETTINGS_RESUME", "closing in-game Settings restores simulation");

            yield return Click(FindSceneComponent<Button>("SaveMenuButton"));
            GameObject saveModal = FindSceneObject("SaveModal");
            Check(saveModal != null && saveModal.activeInHierarchy,
                "SAVE_MENU", "in-game Save opens through pointer input");
            Check(IsActive("InGameActions"), "INGAME_ACTIONS", "Settings, title, and quit actions are visible in-game");

            Button returnButton = FindSceneComponent<Button>("ReturnToTitleButton");
            yield return Click(returnButton);
            Check(saveModal != null && saveModal.activeInHierarchy,
                "RETURN_CONFIRM", "first title return click requests confirmation");
            yield return Click(returnButton);

            yield return WaitForCondition(
                () => SceneManager.GetActiveScene().name == DungeonSceneNavigator.TitleSceneName && FindTitleScope() != null,
                10f);
            yield return new WaitForSecondsRealtime(0.5f);

            titleScope = FindTitleScope();
            titleRoot = FindSceneObject("DungeonTitleRuntimeUI");
            continueButton = FindSceneComponent<Button>("ContinueLatestButton");
            Check(titleScope != null && titleRoot != null && titleRoot.activeInHierarchy,
                "RETURN_TITLE", "return action loads the dedicated title scene");
            if (titleScope == null)
            {
                yield break;
            }

            slotCatalog = titleScope.Container.Resolve<IDungeonSaveSlotCatalog>();
            settings = titleScope.Container.Resolve<IDungeonUserSettingsService>();
            Check(slotCatalog.HasSave(DungeonGameSaveSlotService.AutoSaveSlot),
                "RETURN_AUTOSAVE", "returning to title creates an autosave");
            Check(continueButton != null && continueButton.interactable,
                "CONTINUE_READY", "Continue becomes available after the autosave");
            Check(settings.Current.highContrast,
                "SETTINGS_RELOAD", "accessibility settings survive a scene reload");

            yield return Click(continueButton);
            yield return WaitForCondition(
                () => SceneManager.GetActiveScene().name == DungeonSceneNavigator.GameplaySceneName && FindScope() != null,
                10f);
            yield return new WaitForSecondsRealtime(0.5f);

            scope = FindScope();
            GameObject restoredSaveModal = FindSceneObject("SaveModal");
            ownerManager = FindFirstObjectByType<OwnerRunManager>(FindObjectsInactive.Include);
            Check(scope != null && restoredSaveModal != null && !restoredSaveModal.activeInHierarchy,
                "CONTINUE_LOAD", "Continue restores Gameplay without a title modal");
            Check(ownerManager != null && ownerManager.CurrentOwnerActor != null,
                "CONTINUE_OWNER", "Continue restores the selected owner");
            OwnerSelectionPanel[] restoredOwnerSelections = Resources.FindObjectsOfTypeAll<OwnerSelectionPanel>()
                .Where(panel => panel != null && panel.gameObject.scene.IsValid())
                .ToArray();
            Check(restoredOwnerSelections.All(panel => !panel.gameObject.activeInHierarchy),
                "CONTINUE_OWNER_UI_CLOSED",
                $"restored owner selection no longer blocks HUD pointer input; panels={restoredOwnerSelections.Length}");
            Check(Time.timeScale > 0f, "CONTINUE_RESUME", "Continue restores the running clock");

            IDungeonSceneNavigator gameplayNavigator = scope?.Container.Resolve<IDungeonSceneNavigator>();
            Check(gameplayNavigator != null && gameplayNavigator.LoadGame("qa-missing-slot"),
                "LOAD_FAILURE_REQUEST", "missing-slot launch enters the owned transition path");
            yield return WaitForCondition(
                () => SceneManager.GetActiveScene().name == DungeonSceneNavigator.TitleSceneName && FindTitleScope() != null,
                14f);
            yield return new WaitForSecondsRealtime(0.5f);
            TMP_Text failureStatus = FindSceneComponent<TMP_Text>("TitleStatus");
            Check(FindTitleScope() != null && failureStatus != null && failureStatus.text.Contains("does not exist", StringComparison.Ordinal),
                "LOAD_FAILURE_HANDOFF",
                $"restore failure returns to title with a visible reason; status={failureStatus?.text}");
        }
        finally
        {
            TeardownInput();
            Application.logMessageReceived -= CaptureLog;
            report.Add($"capturedErrors={errors.Count}; capturedWarnings={warnings.Count}");
            foreach (string error in errors)
            {
                report.Add("[CONSOLE ERROR] " + error.Replace('\n', ' '));
            }

            foreach (string warning in warnings)
            {
                report.Add("[CONSOLE WARNING] " + warning.Replace('\n', ' '));
            }

            bool passed = report.All(line => !line.StartsWith("[FAIL]", StringComparison.Ordinal))
                && errors.Count == 0
                && warnings.Count == 0;
            report.Insert(0, passed ? "PRODUCT_SHELL PASS" : "PRODUCT_SHELL FAIL");
            File.WriteAllLines(DungeonProductShellPlayModeVerifier.ReportPath, report);
            File.Delete(DungeonProductShellPlayModeVerifier.RequestPath);
            EditorApplication.ExitPlaymode();
        }
    }

    private IEnumerator Click(Component target)
    {
        Selectable selectable = target as Selectable;
        bool valid = target != null
            && target.gameObject.activeInHierarchy
            && (selectable == null || selectable.interactable);
        Check(valid, "CLICK_TARGET", target != null ? target.name : "<missing>");
        if (!valid)
        {
            yield break;
        }

        string targetName = target.name;
        RectTransform rect = target.GetComponent<RectTransform>();
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        if (Application.isBatchMode
            && PlayModeVerificationFrameWait.DispatchPointerClick(target.gameObject, point))
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield break;
        }

        QueueVerificationMouseState(
            new MouseState { position = point }.WithButton(MouseButton.Left, true));
        yield return null;
        yield return null;
        QueueVerificationMouseState(new MouseState { position = point });
        yield return null;
        yield return null;
        Check(Vector2.Distance(verificationMouse.position.ReadValue(), point) <= 0.1f,
            "POINTER_AT_TARGET",
            $"target={targetName}; expected={point}; actual={verificationMouse.position.ReadValue()}");
    }

    private void QueueVerificationMouseState(MouseState state)
    {
        EnsureVerificationMouse();
        if (verificationMouse == null || !verificationMouse.added)
        {
            return;
        }

        ApplyMouseState(state);
        if (Vector2.Distance(verificationMouse.position.ReadValue(), state.position) <= 0.1f)
        {
            return;
        }

        CreateVerificationMouse();
        ApplyMouseState(state);
    }

    private void EnsureVerificationMouse()
    {
        if (verificationMouse == null || !verificationMouse.added)
        {
            CreateVerificationMouse();
            return;
        }

        if (!verificationMouse.enabled)
        {
            InputSystem.EnableDevice(verificationMouse);
        }

        verificationMouse.MakeCurrent();
    }

    private void CreateVerificationMouse()
    {
        if (verificationMouse != null && verificationMouse.added)
        {
            InputSystem.RemoveDevice(verificationMouse);
        }

        verificationMouse = InputSystem.AddDevice<Mouse>($"ProductShellVerificationMouse{++verificationMouseSerial}");
        InputSystem.EnableDevice(verificationMouse);
        verificationMouse.MakeCurrent();
    }

    private void ApplyMouseState(MouseState state)
    {
        verificationMouse.MakeCurrent();
        InputState.Change(verificationMouse, state);
        InputSystem.QueueStateEvent(verificationMouse, state);
        InputSystem.Update();
        if (Vector2.Distance(verificationMouse.position.ReadValue(), state.position) > 0.1f)
        {
            InputState.Change(verificationMouse, state);
            InputSystem.Update();
        }
    }

    private IEnumerator Capture(string path, string id)
    {
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Texture2D capture = PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        Color32[] pixels = capture != null ? capture.GetPixels32() : Array.Empty<Color32>();
        bool nonblank = pixels.Any(pixel => pixel.a > 0 && (pixel.r > 8 || pixel.g > 8 || pixel.b > 8));
        Check(nonblank, id, $"nonblank pixels={pixels.Length}");
        if (capture != null)
        {
            File.WriteAllBytes(path, capture.EncodeToPNG());
            Destroy(capture);
        }
    }

    private static IEnumerator WaitForCondition(Func<bool> condition, float timeout)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
    }

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return FindObjectsByType<DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null && candidate.Container != null);
    }

    private static DungeonTitleLifetimeScope FindTitleScope()
    {
        return FindObjectsByType<DungeonTitleLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null && candidate.Container != null);
    }

    private static int CountPlayingLoops()
    {
        GameObject audioRoot = FindSceneObject("DungeonAudioRuntime");
        return audioRoot != null
            ? audioRoot.GetComponentsInChildren<AudioSource>(true).Count(source => source.loop && source.isPlaying)
            : 0;
    }

    private static AudioSource FindAudioSource(GameObject root, string name)
    {
        Transform child = root != null ? root.transform.Find(name) : null;
        return child != null ? child.GetComponent<AudioSource>() : null;
    }

    private static bool IsActive(string name)
    {
        GameObject target = FindSceneObject(name);
        return target != null && target.activeInHierarchy;
    }

    private static Button FindTabButton(TabId tabId)
    {
        return Resources.FindObjectsOfTypeAll<UITabButtonBinding>()
            .Where(binding => binding != null
                && binding.gameObject.scene.IsValid()
                && binding.gameObject.activeInHierarchy
                && binding.Id == tabId)
            .Select(binding => binding.GetComponent<Button>())
            .FirstOrDefault(button => button != null);
    }

    private IEnumerator PrepareRecruitCandidate(DungeonRuntimeLifetimeScope scope)
    {
        preparedRecruitCandidate = null;
        preparedRecruitment = null;
        preparedRecruitDiagnostic = string.Empty;
        if (scope == null || scope.Container == null)
        {
            yield break;
        }

        preparedRecruitment = scope.Container.Resolve<RegularCustomerRuntime>();

        ICharacterSpawnerProvider spawnerProvider = scope.Container.Resolve<ICharacterSpawnerProvider>();
        if (!spawnerProvider.TryGetSpawner(out CharacterSpawner spawner)
            || spawner.characterPrefab == null)
        {
            yield break;
        }

        float startedAt = Time.realtimeSinceStartup;
        float deadline = startedAt + 15f;
        bool fixtureSeeded = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            CharacterActor candidate = FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(actor =>
                    RegularCustomerService.IsTrackableCustomer(actor)
                    && !string.IsNullOrWhiteSpace(RegularCustomerService.GetCustomerId(actor)));
            if (candidate != null && candidate.CurrentLifecycleState == CharacterLifecycleState.Active)
            {
                preparedRecruitCandidate = candidate;
                preparedRecruitDiagnostic = fixtureSeeded
                    ? "prepared profile entered through CharacterSpawner"
                    : "background world profile entered naturally";
                break;
            }

            if (!fixtureSeeded && Time.realtimeSinceStartup >= startedAt + 2f)
            {
                fixtureSeeded = TrySeedPreparedRecruitProfile(scope, spawner, out string diagnostic);
                preparedRecruitDiagnostic = diagnostic;
            }

            yield return new WaitForSecondsRealtime(0.1f);
        }

        if (preparedRecruitCandidate == null)
        {
            yield break;
        }

        preparedRecruitCandidate.gameObject.name = "Product Shell Recruit Candidate";
        preparedRecruitCandidate.stats[CharacterCondition.MOOD] = 100f;
        BuildableObject facility = FindObjectsByType<BuildableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(building => building != null && !building.isDestroy);
        int visits = Mathf.Max(1, preparedRecruitment.Rules.recruitCandidateVisitThreshold);
        DungeonStory.Foundation.IGameEventBus gameEventBus =
            scope.Container.Resolve<DungeonStory.Foundation.IGameEventBus>();
        for (int i = 0; i < visits; i++)
        {
            gameEventBus.Publish(new FacilityVisitEvent(preparedRecruitCandidate, facility));
        }
    }

    private static bool TrySeedPreparedRecruitProfile(
        DungeonRuntimeLifetimeScope scope,
        CharacterSpawner spawner,
        out string diagnostic)
    {
        diagnostic = "prepared recruit profile could not be seeded";
        if (scope == null || scope.Container == null || spawner == null)
        {
            return false;
        }

        CharacterSO customerData = spawner.characters?
            .FirstOrDefault(data => data != null
                && data.characterType == CharacterType.Customer
                && data.role != CharacterRole.Owner);
        CharacterActor donor = FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(actor =>
                actor != null
                && !actor.IsOwner
                && actor.Progression != null
                && actor.Progression.ActiveSkills.Count > 0
                && actor.Progression.PassiveSkills.Count > 0);
        if (customerData == null || donor?.Progression == null)
        {
            diagnostic = $"fixture source missing; customer={customerData != null}; donor={donor != null}";
            return false;
        }

        const string FixtureId = "world:product-shell:recruit";
        CharacterGrowthState growth = donor.Progression.GrowthState.Clone();
        growth.initialized = true;
        growth.autoChooseDrafts = true;
        growth.displayName = "검증 방문객";
        growth.origin = customerData.SpeciesTag;
        growth.drafts.Clear();
        growth.pendingRequestKeys.Clear();

        ICharacterPopulationService population =
            scope.Container.Resolve<ICharacterPopulationService>();
        List<WorldCharacterProfile> profiles = population.CaptureProfiles()
            .Where(profile => profile != null
                && !string.Equals(profile.persistentId, FixtureId, StringComparison.Ordinal))
            .ToList();
        profiles.Add(new WorldCharacterProfile
        {
            persistentId = FixtureId,
            characterDataId = customerData.id,
            displayName = growth.displayName,
            origin = growth.origin,
            isAlive = true,
            isVisiting = false,
            level = donor.Progression.Level,
            currentExperience = donor.Progression.CurrentExperience,
            growth = growth,
            narrative = new CharacterNarrativeLedger()
        });
        population.RestoreProfiles(profiles);
        bool spawnAccepted = spawner.TrySpawnCharacter(customerData.id);
        CharacterActor spawned = spawnAccepted
            ? FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(actor => string.Equals(
                    RegularCustomerService.GetCustomerId(actor),
                    FixtureId,
                    StringComparison.Ordinal))
            : null;
        if (spawned != null)
        {
            spawned.transform.position = spawner.GetEntryDoorWorldPosition();
            if (spawned.TryGetAbility(out AbilityMove move))
            {
                move.CancelActiveMovement();
            }

            spawned.SetLifecycleState(CharacterLifecycleState.Active);
        }

        diagnostic = spawnAccepted
            ? $"seeded={FixtureId}; template={customerData.id}; actor={spawned != null}"
            : $"seeded={FixtureId}; CharacterSpawner rejected template={customerData.id}";
        return spawnAccepted;
    }

    private IEnumerator ScrollIntoView(Component target)
    {
        if (target == null)
        {
            yield break;
        }

        ScrollRect scroll = target.GetComponentInParent<ScrollRect>();
        if (scroll == null)
        {
            yield break;
        }

        RectTransform viewport = scroll.viewport != null
            ? scroll.viewport
            : scroll.GetComponent<RectTransform>();
        RectTransform content = scroll.content;
        RectTransform targetRect = target.GetComponent<RectTransform>();
        if (content == null || targetRect == null)
        {
            yield break;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float overflow = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        if (overflow <= 0.1f)
        {
            scroll.verticalNormalizedPosition = 1f;
        }
        else
        {
            Vector3 worldCenter = targetRect.TransformPoint(targetRect.rect.center);
            Vector2 contentPoint = content.InverseTransformPoint(worldCenter);
            float distanceFromTop = content.rect.yMax - contentPoint.y;
            float desiredOffset = Mathf.Clamp(
                distanceFromTop - viewport.rect.height * 0.5f,
                0f,
                overflow);
            scroll.verticalNormalizedPosition = 1f - desiredOffset / overflow;
        }

        yield return null;
        Canvas.ForceUpdateCanvases();
    }

    private static Button FindOffenseExpeditionMemberButton()
    {
        GameObject memberRoot = FindSceneObject("OffenseExpeditionMembers");
        return memberRoot != null
            ? memberRoot.GetComponentsInChildren<Button>(false)
                .FirstOrDefault(button => button != null
                    && button.gameObject.activeInHierarchy
                    && button.interactable
                    && !string.Equals(button.name, "Button_원정 출발", StringComparison.Ordinal)
                    && !string.Equals(button.name, "Button_닫기", StringComparison.Ordinal))
            : null;
    }

    private IEnumerator AdvanceJourneyToFirstBattle(
        OffenseExpeditionRuntime runtime,
        IOffenseBattleRuntime battle)
    {
        int safety = 0;
        while (runtime?.ActiveExpeditions.Count > 0
            && battle != null
            && !battle.HasActiveBattle
            && safety++ < 12)
        {
            OffenseExpeditionRun expedition = runtime.ActiveExpeditions[0];
            Button choice = null;
            if (expedition.Phase == OffenseExpeditionPhase.ChoosingRoute)
            {
                OffenseRouteNode next = expedition.GetAvailableRouteNodes().FirstOrDefault();
                choice = FindActiveButtonContaining(next?.Title);
            }
            else if (expedition.Phase == OffenseExpeditionPhase.ResolvingNode)
            {
                string label = expedition.CurrentNode?.Kind switch
                {
                    OffenseRouteNodeKind.Cache => "보급고 수색",
                    OffenseRouteNodeKind.Camp => "쉬지 않고 전진",
                    _ => "위험 감수"
                };
                choice = FindActiveButtonContaining(label);
            }

            if (choice == null)
            {
                yield break;
            }

            yield return Click(choice);
            yield return null;
        }
    }

    private static Button FindActiveButtonContaining(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return Resources.FindObjectsOfTypeAll<Button>()
            .Where(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.interactable)
            .FirstOrDefault(button => (button.GetComponentInChildren<TMP_Text>(true)?.text ?? string.Empty)
                .Contains(text, StringComparison.Ordinal));
    }

    private static bool IsPanelInsideScreen(string name)
    {
        GameObject panel = FindSceneObject(name);
        if (panel == null || !(panel.transform is RectTransform rect))
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        return min.x >= 0f && min.y >= 0f && max.x <= Screen.width && max.y <= Screen.height;
    }

    private static bool IsUiRaycastBlocking(GameObject modal)
    {
        if (modal == null || EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        return results.Any(result => result.gameObject != null
            && result.gameObject.transform.IsChildOf(modal.transform));
    }

    private static bool IsTopUiTarget(Component target, out string diagnostic)
    {
        if (target == null || EventSystem.current == null)
        {
            diagnostic = target == null
                ? "target=<missing>"
                : "eventSystem=<missing>";
            return false;
        }

        RectTransform rect = target.GetComponent<RectTransform>();
        Vector2 point = RectTransformUtility.WorldToScreenPoint(
            null,
            rect.TransformPoint(rect.rect.center));
        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = point
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        GameObject top = results.FirstOrDefault().gameObject;
        bool matches = top != null
            && (top == target.gameObject
                || top.transform.IsChildOf(target.transform));
        diagnostic = $"target={target.name}; point={point}; top={(top != null ? top.name : "<none>")}";
        return matches;
    }

    private static string DescribeFirstDifference(string expected, string actual)
    {
        expected ??= string.Empty;
        actual ??= string.Empty;
        int sharedLength = Mathf.Min(expected.Length, actual.Length);
        int index = 0;
        while (index < sharedLength && expected[index] == actual[index])
        {
            index++;
        }

        if (index == expected.Length && index == actual.Length)
        {
            return "<none>";
        }

        int start = Mathf.Max(0, index - 48);
        int expectedLength = Mathf.Min(120, expected.Length - start);
        int actualLength = Mathf.Min(120, actual.Length - start);
        string expectedWindow = expected.Substring(start, expectedLength);
        string actualWindow = actual.Substring(start, actualLength);
        return $"index={index}; expected={expectedWindow}; actual={actualWindow}";
    }

    private void CheckStandardThemeLuminance(string id)
    {
        float panel = Luminance(DungeonUiTheme.Panel);
        float surface = Luminance(DungeonUiTheme.Surface);
        float raised = Luminance(DungeonUiTheme.SurfaceRaised);
        bool readable = panel >= 0.34f
            && surface >= panel + 0.07f
            && raised >= surface + 0.09f
            && DungeonUiTheme.ModalScrimAlpha <= 0.36f;
        Check(readable, id,
            $"panel={panel:0.###}; surface={surface:0.###}; raised={raised:0.###}; scrim={DungeonUiTheme.ModalScrimAlpha:0.##}");
    }

    private void CheckHighContrastThemeLuminance(string id)
    {
        float panel = Luminance(DungeonUiTheme.Panel);
        float surface = Luminance(DungeonUiTheme.Surface);
        float raised = Luminance(DungeonUiTheme.SurfaceRaised);
        bool readable = panel >= 0.11f
            && surface >= panel + 0.04f
            && raised >= surface + 0.07f
            && DungeonUiTheme.ModalScrimAlpha <= 0.58f;
        Check(readable, id,
            $"panel={panel:0.###}; surface={surface:0.###}; raised={raised:0.###}; scrim={DungeonUiTheme.ModalScrimAlpha:0.##}");
    }

    private static float Luminance(Color color)
    {
        return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
    }

    private void TeardownInput()
    {
        if (verificationMouse != null && verificationMouse.added)
        {
            InputSystem.RemoveDevice(verificationMouse);
        }

        if (originalMouse != null && originalMouse.added)
        {
            InputSystem.EnableDevice(originalMouse);
            originalMouse.MakeCurrent();
        }

        InputSystem.settings.editorInputBehaviorInPlayMode = originalInputBehavior;
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors.Add(condition + "\n" + stackTrace);
        }
        else if (type == LogType.Warning)
        {
            warnings.Add(condition);
        }
    }

    private void Check(bool condition, string id, string detail)
    {
        report.Add($"[{(condition ? "PASS" : "FAIL")}] {id} {detail}");
    }

    private static IEnumerable<TMP_Text> FindTexts(string value)
    {
        return Resources.FindObjectsOfTypeAll<TMP_Text>()
            .Where(text => text != null && text.gameObject.scene.IsValid() && text.text == value);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<Transform>()
            .Where(candidate => candidate != null && candidate.gameObject.scene.IsValid())
            .Select(candidate => candidate.gameObject)
            .FirstOrDefault(candidate => candidate.name == name);
    }

    private static T FindSceneComponent<T>(string name, string parentName = null) where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(candidate => candidate != null
                && candidate.gameObject.scene.IsValid()
                && candidate.gameObject.name == name
                && (string.IsNullOrEmpty(parentName)
                    || candidate.transform.IsChildOf(FindSceneObject(parentName)?.transform)));
    }
}
