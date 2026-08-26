#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

public static class StartPartyPreparationPlayModeVerifier
{
    public const string ReportPath = "Artifacts/QA/start-party-playmode-report.txt";
    public const string DesktopCapturePath = "Artifacts/QA/start-party-desktop.png";
    public const string MobileCapturePath = "Artifacts/QA/start-party-mobile.png";

    [MenuItem("DungeonStory/Debug/QA/Run Start Party PlayMode Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Start-party verification requires PlayMode in the gameplay scene.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<StartPartyPreparationPlayModeRunner>() != null)
        {
            Debug.LogWarning("Start-party verification is already running.");
            return;
        }

        new GameObject("Start Party PlayMode Verification Runner")
            .AddComponent<StartPartyPreparationPlayModeRunner>();
    }

    public static string RunFastCommitForDebug(string preferredSpeciesTag = null)
    {
        if (!Application.isPlaying)
        {
            return "PlayMode is not active.";
        }

        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        IOwnerRunManagerProvider managerProvider = scope?.Container.Resolve<IOwnerRunManagerProvider>();
        OwnerRunManager manager = managerProvider != null
            && managerProvider.TryGetManager(out OwnerRunManager resolvedManager)
                ? resolvedManager
                : null;
        IStartPartyPreparationService preparation = scope?.Container.Resolve<IStartPartyPreparationService>();
        IPreparedStartPartyCommitService commitService =
            scope?.Container.Resolve<IPreparedStartPartyCommitService>();
        IPreparedStartPartyDiagnosticsQuery diagnosticsQuery =
            scope?.Container.Resolve<IPreparedStartPartyDiagnosticsQuery>();
        CharacterSO ownerData = !string.IsNullOrWhiteSpace(preferredSpeciesTag)
            ? manager?.OwnerCandidates?.FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.SpeciesTag,
                    preferredSpeciesTag,
                    StringComparison.OrdinalIgnoreCase))
            : manager?.OwnerCandidates?.FirstOrDefault();
        ownerData ??= manager?.OwnerCandidates?.FirstOrDefault();
        if (scope == null
            || manager == null
            || preparation == null
            || commitService == null
            || ownerData == null)
        {
            return "Runtime dependencies are missing: "
                + $"scope={scope != null}; container={scope?.Container != null}; "
                + $"managerProvider={managerProvider != null}; manager={manager != null}; "
                + $"preparation={preparation != null}; commit={commitService != null}; "
                + $"diagnostics={diagnosticsQuery != null}; ownerCandidate={ownerData != null}; "
                + $"ownerCandidateCount={manager?.OwnerCandidates?.Count ?? 0}.";
        }

        if (!preparation.Begin(ownerData, out string message))
        {
            return "Begin failed: " + message;
        }

        foreach (StartPartyMemberPreparation member in preparation.Members)
        {
            CharacterSkillDraft draft = member.Progression.Drafts.First(item => item != null
                && item.kind == CharacterSkillKind.Active
                && item.unlockLevel == 1);
            CharacterSkillCandidateRule rule = draft.rules[0];
            draft.candidates = new List<CharacterSkillInstance>
            {
                new CharacterSkillInstance
                {
                    id = $"fast-active-{member.Index}",
                    displayName = $"{member.Progression.GrowthState.displayName}의 기술",
                    description = "빠른 커밋 진단 기술",
                    narrativeReason = "테스트 준비",
                    kind = CharacterSkillKind.Active,
                    rarity = rule.rarity,
                    trigger = rule.trigger,
                    target = rule.target,
                    modules = new List<CharacterSkillModuleSelection>
                    {
                        new CharacterSkillModuleSelection
                        {
                            moduleId = rule.allowedModuleIds.First(),
                            variantId = rule.allowedVariantIds.First()
                        }
                    }
                }
            };
            draft.isReady = true;
            draft.requestSubmitted = false;
            member.Progression.GrowthState.passiveSkills.Add(new CharacterSkillInstance
            {
                id = $"fast-passive-{member.Index}",
                displayName = $"{member.Progression.GrowthState.displayName}의 습관",
                description = "빠른 커밋 진단 패시브",
                narrativeReason = "테스트 준비",
                kind = CharacterSkillKind.Passive,
                rarity = CharacterSkillRarity.Advanced,
                trigger = CharacterSkillTrigger.WorkCompleted,
                target = CharacterSkillTarget.Self,
                modules = new List<CharacterSkillModuleSelection>
                {
                    new CharacterSkillModuleSelection { moduleId = "work_speed", variantId = "small" }
                }
            });
            if (!preparation.TryChooseFirstActive(member.Index, 0, out message))
            {
                return $"Choose failed for {member.Index}: {message}";
            }
        }

        Time.timeScale = 0f;
        bool committed = commitService.TryCommit(out message);
        CharacterActor[] allStaff = CharacterActorCollection.DistinctByGameObject(
            UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            .Where(actor => actor != null
                && actor.Identity != null
                && actor.Identity.PersistentId.StartsWith(
                    "character:staff:",
                    StringComparison.Ordinal))
            .ToArray();
        CharacterActor[] staff = allStaff
            .Where(actor => actor.gameObject.activeInHierarchy)
            .ToArray();
        CharacterActor[] inactiveStaff = allStaff
            .Where(actor => !actor.gameObject.activeInHierarchy)
            .ToArray();
        string actors = string.Join(",", staff.Select(actor =>
            $"{actor.name}:{actor.GetInstanceID()}:{actor.Identity.PersistentId}:active={actor.gameObject.activeInHierarchy}"));
        CharacterSpawner spawner = UnityEngine.Object.FindFirstObjectByType<CharacterSpawner>(
            FindObjectsInactive.Include);
        return $"committed={committed}; message={message}; liveStaff={staff.Length}; "
            + $"inactiveStaffObjects={inactiveStaff.Length}; "
            + $"customerPoolInactive={spawner?.characterPool?.CountInactive ?? 0}; "
            + $"actors={actors}; diagnostics={diagnosticsQuery?.LastReport ?? string.Empty}";
    }
}

public sealed class StartPartyPreparationPlayModeRunner : MonoBehaviour
{
    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private InputSettings.EditorInputBehaviorInPlayMode originalInputBehavior;
    private Mouse originalMouse;
    private Mouse verificationMouse;
    private int originalGameViewSizeIndex = -1;

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory("Artifacts/QA");
        Application.logMessageReceived += CaptureLog;
        SetupInput();
        originalGameViewSizeIndex = GameViewResolutionController.SelectedSizeIndex;
        try
        {
            yield return new WaitForSecondsRealtime(1f);
            DungeonPreparationLifetimeScope preparationScope =
                FindFirstObjectByType<DungeonPreparationLifetimeScope>();
            IStartPartyPreparationService preparation = preparationScope?.Container?
                .Resolve<IStartPartyPreparationService>();
            Check(preparationScope != null && preparation != null,
                "PREPARATION_SCOPE",
                "dedicated preparation scene and service resolved");
            Check(preparation != null && !preparation.IsPreparing,
                "FRESH_RUN",
                "verification starts before a party is committed");

            Button owner = FindButtonPrefix("OwnerCandidate_", true);
            Check(owner != null, "OWNER_OPTION", "owner candidate visible");
            if (owner == null)
            {
                yield break;
            }

            yield return Click(owner);
            yield return Click(FindButton("PreparationOwnerNextButton", true));
            yield return new WaitForSecondsRealtime(0.25f);
            Check(FindButtonsPrefix("PreparationRosterCard_").Length == 7
                    && FindButtonsPrefix("PreparationTab_").Length == 3,
                "ROSTER_AND_DETAIL_TABS",
                "selected and reserve roster exposes one RimWorld-style detail surface");
            Check(FindButtonsPrefix("PartyBackToOwnerButton").Length == 1
                && FindButtonsPrefix("PreparationStartRunButton").Length == 1,
                "SINGLE_ACTION_ROW",
                "preparation actions are not duplicated");

            StartPartyMemberPreparation selectedBeforeDrag = preparation?.Members
                .FirstOrDefault(member => member != null && !member.IsOwner);
            StartPartyMemberPreparation reserveBeforeDrag = preparation?.Reserves
                .FirstOrDefault(member => member != null);
            int selectedSlotBeforeDrag = selectedBeforeDrag?.PartySlot ?? -1;
            Button selectedCard = selectedBeforeDrag != null
                ? FindButton($"PreparationRosterCard_{selectedBeforeDrag.Index}", true)
                : null;
            Button reserveCard = reserveBeforeDrag != null
                ? FindButton($"PreparationRosterCard_{reserveBeforeDrag.Index}", true)
                : null;
            Check(selectedCard != null && reserveCard != null,
                "ROSTER_DRAG_TARGETS",
                "selected and reserve cards accept pointer input");
            if (selectedCard != null && reserveCard != null)
            {
                yield return Drag(selectedCard, reserveCard);
                StartPartyMemberPreparation draggedOut = preparation.Roster
                    .FirstOrDefault(member => member != null
                        && member.Index == selectedBeforeDrag.Index);
                StartPartyMemberPreparation draggedIn = preparation.Roster
                    .FirstOrDefault(member => member != null
                        && member.Index == reserveBeforeDrag.Index);
                Check(draggedOut != null
                        && draggedOut.IsReserve
                        && draggedIn != null
                        && !draggedIn.IsReserve
                        && draggedIn.PartySlot == selectedSlotBeforeDrag,
                    "ROSTER_DRAG_SWAP",
                    $"out={draggedOut?.RosterLabel}; in={draggedIn?.RosterLabel}; slot={draggedIn?.PartySlot}");
            }

            int selectedStaffIndex = preparation?.Members
                .FirstOrDefault(member => member != null && !member.IsOwner)?.Index ?? 1;
            yield return Click(FindButton($"PreparationRosterCard_{selectedStaffIndex}", true));
            yield return Click(FindButton(
                $"PreparationTab_{selectedStaffIndex}_Identity",
                true));
            Button partial = FindButton(
                $"PreparationIdentityRerollDice_{selectedStaffIndex}",
                true);
            Check(partial != null, "PARTIAL_REROLL", "identity dice reroll visible");
            if (partial != null)
            {
                yield return Click(partial);
                partial = FindButton(
                    $"PreparationIdentityRerollDice_{selectedStaffIndex}",
                    false);
                Check(GetLabel(partial).Contains("2"),
                    "PARTIAL_CHARGE",
                    GetLabel(partial));
            }

            Button full = FindButton(
                $"PreparationFullRerollDice_{selectedStaffIndex}",
                true);
            Check(full != null, "FULL_REROLL", "full dice reroll visible");
            if (full != null)
            {
                yield return Click(full);
                partial = FindButton(
                    $"PreparationIdentityRerollDice_{selectedStaffIndex}",
                    false);
                Check(GetLabel(partial).Contains("3"),
                    "FULL_RECHARGE",
                    GetLabel(partial));
            }

            foreach (StartPartyMemberPreparation member in preparation.Members)
            {
                yield return Click(FindButton($"PreparationRosterCard_{member.Index}", true));
                Button skillTab = FindButton($"PreparationTab_{member.Index}_Skill", true);
                Check(skillTab != null, $"SKILL_TAB_{member.Index}", "skill tab visible");
                if (skillTab != null)
                {
                    yield return Click(skillTab);
                }
            }

            yield return WaitForGeneratedStartSkills(30f);
            Check(!VisibleTextContains("LLM")
                && !VisibleTextContains("생성 중")
                && !VisibleTextContains("요청 키"),
                "NO_TECHNICAL_GENERATION_TEXT",
                "generation internals are hidden from the player");

            yield return SelectResolution(new Vector2Int(1600, 900), "DESKTOP_RESOLUTION");
            yield return Capture(
                StartPartyPreparationPlayModeVerifier.DesktopCapturePath,
                "DESKTOP_CAPTURE",
                new Vector2Int(1600, 900));

            Check(FindButtonsPrefix("StartSkillCandidate_").Length == 0,
                "NO_START_SKILL_CHOICES",
                "first actives are generated automatically instead of selected");
            Check(FindGeneratedSkillCards().Length >= 2,
                "GENERATED_START_SKILLS",
                "generated active and passive cards are visible for the selected staff");

            yield return WaitForPartyReady(180f);
            Button confirm = FindButton("PreparationStartRunButton", true);
            Check(confirm != null, "PARTY_READY", "all three selections unlock the start command");

            yield return SelectResolution(new Vector2Int(900, 1600), "MOBILE_RESOLUTION");
            RectTransform[] memberCards = FindMemberCards();
            Check(memberCards.Length == 7 && memberCards.All(IsInsideScreen),
                "MOBILE_BOUNDS",
                "all party cards remain inside the portrait viewport");
            yield return Capture(
                StartPartyPreparationPlayModeVerifier.MobileCapturePath,
                "MOBILE_CAPTURE",
                new Vector2Int(900, 1600));

            if (confirm != null)
            {
                confirm = FindButton("PreparationStartRunButton", true);
                yield return Click(confirm);
                float gameplayDeadline = Time.realtimeSinceStartup + 12f;
                while (SceneManager.GetActiveScene().name != DungeonSceneNavigator.GameplaySceneName
                    && Time.realtimeSinceStartup < gameplayDeadline)
                {
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(0.75f);
            }

            OwnerRunManager ownerManager = FindFirstObjectByType<OwnerRunManager>();
            CharacterActor ownerActor = ownerManager?.CurrentOwnerActor;
            CharacterActor[] staff = CharacterActorCollection.DistinctByGameObject(
                FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
                .Where(actor => actor != null
                    && actor.Identity != null
                    && actor.Identity.PersistentId.StartsWith(
                        "character:staff:",
                        StringComparison.Ordinal))
                .ToArray();
            Check(ownerActor != null && staff.Length == 2,
                "PARTY_COMMITTED",
                $"owner={ownerActor != null}, staff={staff.Length}");
            Check(ownerActor != null
                && staff.All(actor => string.Equals(actor.SpeciesTag, ownerActor.SpeciesTag, StringComparison.OrdinalIgnoreCase)),
                "SAME_SPECIES",
                ownerActor != null ? ownerActor.SpeciesTag : "owner missing");
            Check(ownerActor?.Progression != null
                    && ownerActor.Progression.OwnerFixedSkills.Count
                        == CharacterOwnerFixedSkillUtility.FixedSlotCount,
                "OWNER_FIXED_SKILLS",
                $"owner fixed skills={ownerActor?.Progression?.OwnerFixedSkills.Count ?? 0}");
            Check(staff.All(actor => actor.Progression != null
                    && actor.Progression.ActiveSkills.Count == 1
                    && actor.Progression.PassiveSkills.Count == 1),
                "STAFF_READY_SKILLS",
                string.Join(", ", staff.Select(actor =>
                    $"{actor.name}: active={actor.Progression?.ActiveSkills.Count ?? 0}, "
                    + $"passive={actor.Progression?.PassiveSkills.Count ?? 0}")));
            Check(FindButton("PreparationStartRunButton", false) == null,
                "PREPARATION_CLOSED",
                "preparation UI closes after commit");
        }
        finally
        {
            if (originalGameViewSizeIndex >= 0)
            {
                GameViewResolutionController.SelectedSizeIndex = originalGameViewSizeIndex;
            }
            TeardownInput();
            Application.logMessageReceived -= CaptureLog;
            Finish();
            Destroy(gameObject);
            EditorApplication.ExitPlaymode();
        }
    }

    private IEnumerator WaitForGeneratedStartSkills(float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (FindButton("PreparationStartRunButton", true) != null
                && FindGeneratedSkillCards().Length >= 2)
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.25f);
        }

        failures.Add($"GENERATED_SKILL_TIMEOUT: generated start skills were not ready within {timeoutSeconds:0.#} seconds");
    }

    private IEnumerator WaitForPartyReady(float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (FindButton("PreparationStartRunButton", true) != null)
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.25f);
        }

        failures.Add($"PARTY_READY_TIMEOUT: first passives were not ready within {timeoutSeconds:0.#} seconds");
    }

    private IEnumerator Click(Button button)
    {
        if (button == null || verificationMouse == null)
        {
            yield break;
        }

        RectTransform rect = button.transform as RectTransform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(
            null,
            rect != null ? rect.TransformPoint(rect.rect.center) : button.transform.position);
        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(
            verificationMouse,
            new MouseState { position = point }.WithButton(MouseButton.Left, true));
        yield return null;
        yield return null;
        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(verificationMouse, new MouseState { position = point });
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
    }

    private IEnumerator Drag(Button source, Button target)
    {
        if (source == null || target == null || verificationMouse == null)
        {
            yield break;
        }

        RectTransform sourceRect = source.transform as RectTransform;
        RectTransform targetRect = target.transform as RectTransform;
        Vector2 sourcePoint = RectTransformUtility.WorldToScreenPoint(
            null,
            sourceRect != null ? sourceRect.TransformPoint(sourceRect.rect.center) : source.transform.position);
        Vector2 targetPoint = RectTransformUtility.WorldToScreenPoint(
            null,
            targetRect != null ? targetRect.TransformPoint(targetRect.rect.center) : target.transform.position);
        Vector2 midpoint = Vector2.Lerp(sourcePoint, targetPoint, 0.5f);

        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(
            verificationMouse,
            new MouseState { position = sourcePoint }.WithButton(MouseButton.Left, true));
        yield return null;
        yield return null;

        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(
            verificationMouse,
            new MouseState { position = midpoint }.WithButton(MouseButton.Left, true));
        yield return null;
        yield return null;

        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(
            verificationMouse,
            new MouseState { position = targetPoint }.WithButton(MouseButton.Left, true));
        yield return null;
        yield return null;

        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(verificationMouse, new MouseState { position = targetPoint });
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
    }

    private IEnumerator SelectResolution(Vector2Int resolution, string id)
    {
        GameViewResolutionController.Select(resolution.x, resolution.y);
        float deadline = Time.realtimeSinceStartup + 3f;
        while ((Screen.width != resolution.x || Screen.height != resolution.y)
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        Check(Screen.width == resolution.x && Screen.height == resolution.y,
            id,
            $"actual={Screen.width}x{Screen.height}");
    }

    private IEnumerator Capture(string path, string id, Vector2Int expectedSize)
    {
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Texture2D capture = PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        Color32[] pixels = capture != null ? capture.GetPixels32() : Array.Empty<Color32>();
        bool nonBlank = pixels.Any(pixel => pixel.a > 0 && (pixel.r > 8 || pixel.g > 8 || pixel.b > 8));
        bool expectedDimensions = capture != null
            && capture.width == expectedSize.x
            && capture.height == expectedSize.y;
        Check(nonBlank && expectedDimensions,
            id,
            capture != null
                ? $"size={capture.width}x{capture.height}; pixels={pixels.Length}"
                : "capture missing");
        if (capture != null)
        {
            File.WriteAllBytes(path, capture.EncodeToPNG());
            Destroy(capture);
        }
    }

    private void SetupInput()
    {
        originalInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        originalMouse = Mouse.current;
        if (originalMouse != null)
        {
            InputSystem.DisableDevice(originalMouse);
        }

        verificationMouse = InputSystem.AddDevice<Mouse>("StartPartyVerificationMouse");
        verificationMouse.MakeCurrent();
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

    private static Button FindButton(string name, bool requireInteractable)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && string.Equals(button.name, name, StringComparison.Ordinal)
                && (!requireInteractable || button.interactable));
    }

    private static Button FindButtonPrefix(string prefix, bool requireInteractable)
    {
        return FindButtonsPrefix(prefix)
            .FirstOrDefault(button => !requireInteractable || button.interactable);
    }

    private static Button[] FindButtonsPrefix(string prefix)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .Where(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.name.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
    }

    private static RectTransform[] FindMemberCards()
    {
        return Resources.FindObjectsOfTypeAll<RectTransform>()
            .Where(rect => rect != null
                && rect.gameObject.scene.IsValid()
                && rect.gameObject.activeInHierarchy
                && rect.name.StartsWith("PreparationRosterCard_", StringComparison.Ordinal))
            .ToArray();
    }

    private static RectTransform[] FindGeneratedSkillCards()
    {
        return Resources.FindObjectsOfTypeAll<RectTransform>()
            .Where(rect => rect != null
                && rect.gameObject.scene.IsValid()
                && rect.gameObject.activeInHierarchy
                && rect.name.StartsWith("OwnerSkillCard_", StringComparison.Ordinal))
            .ToArray();
    }

    private static bool IsInsideScreen(RectTransform rect)
    {
        if (rect == null)
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners.All(corner => corner.x >= -0.5f
            && corner.y >= -0.5f
            && corner.x <= Screen.width + 0.5f
            && corner.y <= Screen.height + 0.5f);
    }

    private static string GetLabel(Button button)
    {
        return button != null
            ? button.GetComponentInChildren<TMP_Text>(true)?.text ?? string.Empty
            : string.Empty;
    }

    private static bool VisibleTextContains(string value)
    {
        return FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Any(text => text != null && text.text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private void Check(bool condition, string id, string detail)
    {
        report.Add($"{(condition ? "PASS" : "FAIL")} {id}: {detail}");
        if (!condition)
        {
            failures.Add($"{id}: {detail}");
        }
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            warnings.Add(condition);
        }
        else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors.Add(condition + "\n" + stackTrace);
        }
    }

    private void Finish()
    {
        report.Add($"errors={errors.Count}; warnings={warnings.Count}; failures={failures.Count}");
        if (errors.Count > 0) report.Add("ERRORS: " + string.Join(" || ", errors));
        if (warnings.Count > 0) report.Add("WARNINGS: " + string.Join(" || ", warnings));
        if (failures.Count > 0) report.Add("FAILURES: " + string.Join(" || ", failures));
        File.WriteAllLines(StartPartyPreparationPlayModeVerifier.ReportPath, report);
        if (failures.Count == 0 && errors.Count == 0 && warnings.Count == 0)
        {
            Debug.Log("Start-party PlayMode verification passed.");
        }
        else
        {
            Debug.LogError("Start-party PlayMode verification failed. See "
                + StartPartyPreparationPlayModeVerifier.ReportPath);
        }
    }
}
#endif
