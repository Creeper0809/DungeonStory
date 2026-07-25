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
            return "Runtime dependencies are missing.";
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
        CharacterActor[] staff = CharacterActorCollection.DistinctByGameObject(
            UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            .Where(actor => actor != null
                && actor.Identity != null
                && actor.Identity.PersistentId.StartsWith("staff:", StringComparison.Ordinal))
            .ToArray();
        string actors = string.Join(",", staff.Select(actor =>
            $"{actor.name}:{actor.GetInstanceID()}:{actor.Identity.PersistentId}:active={actor.gameObject.activeInHierarchy}"));
        return $"committed={committed}; message={message}; staff={staff.Length}; actors={actors}; diagnostics={StartPartyCommitDiagnostics.LastReport}";
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
        Directory.CreateDirectory("Artifacts/QA");
        Application.logMessageReceived += CaptureLog;
        SetupInput();
        originalGameViewSizeIndex = GameViewResolutionController.SelectedSizeIndex;
        try
        {
            yield return new WaitForSecondsRealtime(1f);
            OwnerRunManager ownerManager = FindFirstObjectByType<OwnerRunManager>();
            Check(ownerManager != null, "OWNER_MANAGER", "owner manager resolved");
            Check(ownerManager != null && ownerManager.CurrentOwnerActor == null,
                "FRESH_RUN",
                "verification starts before a party is committed");

            Button owner = FindButtonPrefix("OwnerOption_", true);
            Check(owner != null, "OWNER_OPTION", "owner candidate visible");
            if (owner == null)
            {
                yield break;
            }

            yield return Click(owner);
            yield return new WaitForSecondsRealtime(0.25f);
            Check(FindButtonsPrefix("PreparationTab_").Length == 9,
                "THREE_MEMBER_TABS",
                "three members expose identity, aptitude, and skill tabs");
            Check(FindButtonsPrefix("PartyBackToOwnerButton").Length == 1
                && FindButtonsPrefix("PreparationStartRunButton").Length == 1,
                "SINGLE_ACTION_ROW",
                "preparation actions are not duplicated");

            Button partial = FindButton("PartialReroll_0_Identity", true);
            Check(partial != null, "PARTIAL_REROLL", "identity partial reroll visible");
            if (partial != null)
            {
                yield return Click(partial);
                partial = FindButton("PartialReroll_0_Identity", false);
                Check(GetLabel(partial).Contains("2/3"),
                    "PARTIAL_CHARGE",
                    GetLabel(partial));
            }

            Button full = FindButton("FullReroll_0", true);
            Check(full != null, "FULL_REROLL", "full reroll visible");
            if (full != null)
            {
                yield return Click(full);
                partial = FindButton("PartialReroll_0_Identity", false);
                Check(GetLabel(partial).Contains("3/3"),
                    "FULL_RECHARGE",
                    GetLabel(partial));
            }

            for (int memberIndex = 0; memberIndex < 3; memberIndex++)
            {
                Button skillTab = FindButton($"PreparationTab_{memberIndex}_Skill", true);
                Check(skillTab != null, $"SKILL_TAB_{memberIndex}", "skill tab visible");
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
            Check(FindMemberCards().All(IsInsideScreen),
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
                yield return new WaitForSecondsRealtime(0.75f);
            }

            CharacterActor ownerActor = ownerManager?.CurrentOwnerActor;
            CharacterActor[] staff = CharacterActorCollection.DistinctByGameObject(
                FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
                .Where(actor => actor != null
                    && actor.Identity != null
                    && actor.Identity.PersistentId.StartsWith("staff:", StringComparison.Ordinal))
                .ToArray();
            Check(ownerActor != null && staff.Length == 2,
                "PARTY_COMMITTED",
                $"owner={ownerActor != null}, staff={staff.Length}");
            Check(ownerActor != null
                && staff.All(actor => string.Equals(actor.SpeciesTag, ownerActor.SpeciesTag, StringComparison.OrdinalIgnoreCase)),
                "SAME_SPECIES",
                ownerActor != null ? ownerActor.SpeciesTag : "owner missing");
            Check(ownerActor != null
                && new[] { ownerActor }.Concat(staff).All(actor => actor.Progression != null
                    && actor.Progression.ActiveSkills.Count == 1
                    && actor.Progression.PassiveSkills.Count == 1),
                "READY_SKILLS",
                "owner and staff each retain one confirmed active and first passive");
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
                && rect.name.StartsWith("StartPartyMember_", StringComparison.Ordinal))
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
