#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using DungeonStory.Foundation;

public static class StartPartyPreparationPlayModeVerifier
{
    public const string ReportPath = "Artifacts/QA/start-party-playmode-report.txt";
    public const string DesktopCapturePath = "Artifacts/QA/start-party-desktop.png";
    public const string MobileCapturePath = "Artifacts/QA/start-party-mobile.png";
    public const string OwnerTraitUpgradeFocusedReportPath =
        "Artifacts/QA/wim-implementation/"
        + "wim-010-starting-owner-trait-upgrade.txt";

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

    [MenuItem("DungeonStory/QA/Run WIM-010 Starting Owner Trait Focused")]
    public static void RunOwnerTraitUpgradeFocusedFromMenu() =>
        RunOwnerTraitUpgradeFocused();

    public static string RunOwnerTraitUpgradeFocused()
    {
        if (!Application.isPlaying)
        {
            return "FAILED: WIM-010 focused verification requires PlayMode in GameplayScene.";
        }

        if (UnityEngine.Object.FindFirstObjectByType<
                StartPartyOwnerTraitUpgradeFocusedRunner>() != null)
        {
            return "ALREADY_RUNNING: WIM-010 starting-owner-trait focused verifier";
        }

        GameObject host = new("WIM-010 Starting Owner Trait Focused Verifier");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<StartPartyOwnerTraitUpgradeFocusedRunner>();
        return "STARTED: WIM-010 starting-owner-trait focused verifier";
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

        if (!TryReconcileTierZeroForDirectGameplayFixture(
                scope,
                out string tierZeroDetail))
        {
            return "Tier-zero reconciliation failed: " + tierZeroDetail;
        }

        if (!preparation.Begin(ownerData, out string message))
        {
            return "Begin failed: " + message;
        }

        foreach (StartPartyMemberPreparation member in preparation.Members
                     .Where(candidate => candidate != null
                         && !candidate.IsOwner
                         && !candidate.IsReserve))
        {
            CharacterSkillDraft draft = member.Progression.Drafts.First(item => item != null
                && item.kind == CharacterSkillKind.Active
                && item.unlockLevel == 1);
            draft.candidates = new List<CharacterSkillInstance>
            {
                new CharacterSkillInstance
                {
                    id = $"fast-active-{member.Index}",
                    displayName = $"{member.Progression.GrowthState.displayName}의 기술",
                    description = "빠른 커밋 진단 기술",
                    narrativeReason = "테스트 준비",
                    kind = CharacterSkillKind.Active,
                    rarity = CharacterSkillRarity.Advanced,
                    trigger = CharacterSkillTrigger.ManualCombat,
                    target = CharacterSkillTarget.Enemy,
                    modules = new List<CharacterSkillModuleSelection>
                    {
                        new CharacterSkillModuleSelection
                        {
                            moduleId = "damage",
                            variantId = "light"
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
        return $"committed={committed}; message={message}; tierZero={tierZeroDetail}; liveStaff={staff.Length}; "
            + $"inactiveStaffObjects={inactiveStaff.Length}; "
            + $"customerPoolInactive={spawner?.characterPool?.CountInactive ?? 0}; "
            + $"actors={actors}; diagnostics={diagnosticsQuery?.LastReport ?? string.Empty}";
    }

    public static bool TryReconcileTierZeroForDirectGameplayFixture(
        DungeonRuntimeLifetimeScope scope,
        out string detail)
    {
        detail = string.Empty;
        if (scope?.Container == null)
        {
            detail = "runtime-container-missing";
            return false;
        }

        try
        {
            IDungeonSpaceExpansionQuery query =
                scope.Container.Resolve<IDungeonSpaceExpansionQuery>();
            IDungeonSpaceExpansionCommand command =
                scope.Container.Resolve<IDungeonSpaceExpansionCommand>();
            if (query == null || command == null)
            {
                detail = $"expansion-authority-missing:query={query != null};command={command != null}";
                return false;
            }
            if (!query.TryCaptureLayout(
                    out DungeonInteriorLayoutSnapshot before,
                    out string beforeFailure))
            {
                detail = "pre-layout-capture-failed:" + beforeFailure;
                return false;
            }
            if (before.ColumnCount
                is not (DungeonSpaceExpansionCatalog.SceneSeedInteriorColumns
                    or DungeonSpaceExpansionCatalog.InitialInteriorColumns))
            {
                detail = "noncanonical-pre-layout:" + before.ColumnCount;
                return false;
            }
            if (!command.TryReconcileNewRunTierZero(
                    out DungeonSpaceExpansionResult result,
                    out string reconcileFailure))
            {
                detail = "production-reconcile-failed:" + reconcileFailure;
                return false;
            }

            bool expectedChanged = before.ColumnCount
                == DungeonSpaceExpansionCatalog.SceneSeedInteriorColumns;
            bool resultExact = string.Equals(
                    result.ResearchProjectId,
                    DungeonSpaceExpansionCatalog.TierZeroInitializationId,
                    StringComparison.Ordinal)
                && result.Tier == 0
                && result.PreviousInteriorColumns == before.ColumnCount
                && result.CurrentInteriorColumns
                    == DungeonSpaceExpansionCatalog.InitialInteriorColumns
                && result.Changed == expectedChanged;
            if (!resultExact)
            {
                detail = $"noncanonical-result:id={result.ResearchProjectId};tier={result.Tier};"
                    + $"changed={result.Changed}/{expectedChanged};"
                    + $"columns={result.PreviousInteriorColumns}->{result.CurrentInteriorColumns}";
                return false;
            }
            if (!query.TryCaptureLayout(
                    out DungeonInteriorLayoutSnapshot after,
                    out string afterFailure)
                || after.ColumnCount
                    != DungeonSpaceExpansionCatalog.InitialInteriorColumns
                || after.StartX != before.StartX
                || after.EntrancePosition != before.EntrancePosition)
            {
                detail = "post-layout-capture-failed:"
                    + (string.IsNullOrWhiteSpace(afterFailure)
                        ? $"columns={after.ColumnCount};startX={after.StartX};entrance={after.EntrancePosition}"
                        : afterFailure);
                return false;
            }

            detail = $"changed={result.Changed};columns={before.ColumnCount}->{after.ColumnCount};"
                + $"startX={after.StartX};entrance={after.EntrancePosition}";
            return true;
        }
        catch (Exception exception)
        {
            detail = exception.GetType().Name + ":" + exception.Message;
            return false;
        }
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
            IStartingOwnerTraitCountBonusQuery traitBonusQuery = preparationScope?.Container?
                .Resolve<IStartingOwnerTraitCountBonusQuery>();
            int ownerTraitCountBonus = 0;
            string traitBonusFailure = "trait-bonus-query-missing";
            bool traitBonusResolved = traitBonusQuery != null
                && traitBonusQuery.TryGetStartingOwnerTraitCountBonus(
                    out ownerTraitCountBonus,
                    out traitBonusFailure);
            Check(traitBonusResolved && ownerTraitCountBonus is 0 or 1,
                "OWNER_TRAIT_BONUS_SNAPSHOT",
                traitBonusResolved
                    ? $"bonus={ownerTraitCountBonus}"
                    : $"failure={traitBonusFailure}");
            StartPartyMemberPreparation preparedOwner = preparation?.Members
                .FirstOrDefault(member => member != null && member.IsOwner);
            int[] preparedOwnerTraitIds = preparedOwner?.Progression?.GrowthState?.traitIds
                ?.ToArray() ?? Array.Empty<int>();
            string preparedStaffTraitCounts = string.Join(",",
                preparation?.Members
                    .Where(member => member != null && !member.IsOwner)
                    .Select(member =>
                        member.Progression?.GrowthState?.traitIds?.Count ?? -1)
                ?? Array.Empty<int>());
            Check(preparedOwner != null
                    && preparedOwner.IsOwnerLocked
                    && preparedOwnerTraitIds.Length >= 1 + ownerTraitCountBonus
                    && preparedOwnerTraitIds.Length <= 4 + ownerTraitCountBonus
                    && preparedOwnerTraitIds.Distinct().Count() == preparedOwnerTraitIds.Length
                    && preparation.Members.Where(member => member != null && !member.IsOwner)
                        .All(member => member.Progression?.GrowthState?.traitIds?.Count is >= 1 and <= 4),
                "PREPARED_OWNER_TRAIT_COUNT",
                $"owner={preparedOwnerTraitIds.Length}; bonus={ownerTraitCountBonus}; "
                + $"staff={preparedStaffTraitCounts}");
            VerifyOwnerTraitCountRuleCeilings(
                preparationScope,
                preparedOwner);
            yield return Click(FindButton("PreparationRosterCard_0", true));
            yield return Click(FindButton("PreparationTab_0_Identity", true));
            TMP_Text ownerIdentityText = FindMemberBodyText(0);
            Check(ownerIdentityText != null
                    && preparedOwner?.Progression.ResolveSelectedTraits()
                        .Where(trait => trait != null)
                        .All(trait => ownerIdentityText.text.Contains(
                            trait.traitName,
                            StringComparison.Ordinal)) == true,
                "OWNER_TRAIT_IDENTITY_UI",
                $"traits={preparedOwnerTraitIds.Length}; textFound={ownerIdentityText != null}");
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
            Check(preparedOwner != null
                    && preparedOwner.IsOwnerLocked
                    && preparedOwner.Progression.GrowthState.traitIds.SequenceEqual(
                        preparedOwnerTraitIds),
                "OWNER_TRAIT_LOCKED_DURING_STAFF_REROLLS",
                $"owner={string.Join(",", preparedOwner?.Progression?.GrowthState?.traitIds ?? new List<int>())}");

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
            VerifyOwnerTraitCommitProjectionAndCurrentSave(
                ownerActor,
                staff,
                preparedOwnerTraitIds,
                ownerTraitCountBonus);
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

    private void VerifyOwnerTraitCountRuleCeilings(
        DungeonPreparationLifetimeScope preparationScope,
        StartPartyMemberPreparation preparedOwner)
    {
        if (preparationScope?.Container == null)
        {
            Check(false, "OWNER_TRAIT_RULE_CEILINGS", "preparation container missing");
            return;
        }

        try
        {
            IGameContentCatalog content = preparationScope.Container
                .Resolve<IGameContentCatalog>();
            ICharacterSkillSystemSettingsProvider settings = preparationScope.Container
                .Resolve<ICharacterSkillSystemSettingsProvider>();
            ICharacterRuntimeProfileFactory profiles = preparationScope.Container
                .Resolve<ICharacterRuntimeProfileFactory>();
            string speciesTag = preparedOwner?.CharacterData?.SpeciesTag ?? string.Empty;
            CharacterTraitSO[] traits = content.GetAll<CharacterTraitSO>()
                .Where(trait => trait != null)
                .ToArray();
            int[] ownerFive = CharacterTraitSelectionRules.Select(
                    traits,
                    settings.Settings.traitConflicts,
                    new TraitCeilingProbeRandom(),
                    speciesTag,
                    maximumCount: 5,
                    traitCountBonus: 1)
                .ToArray();
            int[] staffFour = CharacterTraitSelectionRules.Select(
                    traits,
                    settings.Settings.traitConflicts,
                    new TraitCeilingProbeRandom(),
                    speciesTag,
                    maximumCount: 4)
                .ToArray();
            CharacterTraitSO[] ownerFiveTraits = ownerFive
                .Select(id => traits.Single(trait => trait.id == id))
                .ToArray();
            CharacterRuntimeProfile ownerFiveProfile = preparedOwner?.CharacterData != null
                ? profiles.Create(CharacterSpawnRequest.FromAuthoring(
                    preparedOwner.CharacterData,
                    ownerFiveTraits))
                : null;
            CharacterGrowthState ownerFiveGrowth = preparedOwner?.Progression?
                .GrowthState.Clone();
            if (ownerFiveGrowth != null)
            {
                ownerFiveGrowth.traitIds = ownerFive.ToList();
            }

            DungeonCharacterSaveData ownerFiveSave = new()
            {
                persistentId = "character:owner-trait-five-probe",
                isOwner = true,
                displayName = preparedOwner?.Progression?.GrowthState?.displayName
                    ?? "Owner trait probe",
                growth = ownerFiveGrowth
            };
            DungeonCharacterSaveData reloadedOwnerFive = JsonUtility
                .FromJson<DungeonCharacterSaveData>(
                    JsonUtility.ToJson(ownerFiveSave));
            DungeonGameRestoreReport ownerFiveSaveReport = new();
            CharacterWorldSaveValidation.ValidateActor(
                reloadedOwnerFive,
                reloadedOwnerFive.persistentId,
                ownerFiveSaveReport);
            Check(ownerFive.Length == 5
                    && ownerFive.Distinct().Count() == ownerFive.Length
                    && staffFour.Length == 4
                    && staffFour.Distinct().Count() == staffFour.Length
                    && ownerFiveProfile?.ExpressedTraitIds.Count == 5
                    && ownerFiveSaveReport.Success
                    && reloadedOwnerFive.growth.traitIds.Count == 5,
                "OWNER_TRAIT_RULE_CEILINGS",
                $"ownerBonusOne={ownerFive.Length}; staff={staffFour.Length}; "
                + $"profile={ownerFiveProfile?.ExpressedTraitIds.Count ?? -1}; "
                + $"save={reloadedOwnerFive.growth.traitIds.Count}; "
                + $"errors={string.Join("|", ownerFiveSaveReport.Errors)}");
        }
        catch (Exception exception)
        {
            Check(false,
                "OWNER_TRAIT_RULE_CEILINGS",
                exception.GetType().Name + ":" + exception.Message);
        }
    }

    private void VerifyOwnerTraitCommitProjectionAndCurrentSave(
        CharacterActor owner,
        IReadOnlyList<CharacterActor> staff,
        IReadOnlyList<int> preparedOwnerTraitIds,
        int ownerTraitCountBonus)
    {
        int[] actualOwnerTraitIds = owner?.Progression?.GrowthState?.traitIds
            ?.ToArray() ?? Array.Empty<int>();
        Check(owner?.Progression != null
                && actualOwnerTraitIds.SequenceEqual(preparedOwnerTraitIds)
                && actualOwnerTraitIds.Length <= 4 + ownerTraitCountBonus,
            "OWNER_TRAIT_COMMIT",
            $"prepared={string.Join(",", preparedOwnerTraitIds ?? Array.Empty<int>())}; "
            + $"actor={string.Join(",", actualOwnerTraitIds)}; bonus={ownerTraitCountBonus}");
        Check(staff.All(actor => actor?.Progression?.GrowthState?.traitIds?.Count is >= 1 and <= 4),
            "STAFF_TRAIT_LIMIT",
            string.Join(",", staff.Select(actor =>
                actor?.Progression?.GrowthState?.traitIds?.Count ?? -1)));

        try
        {
            CharacterRuntimeProfile effective = owner?.Progression?.GetEffectiveRuntimeProfile();
            string[] selectedTraitIds = owner?.Progression?.ResolveSelectedTraits()
                .Where(trait => trait != null)
                .Select(trait => trait.DefinitionId.Value)
                .ToArray() ?? Array.Empty<string>();
            Check(effective != null
                    && effective.ExpressedTraitIds.SequenceEqual(
                        selectedTraitIds,
                        StringComparer.Ordinal),
                "OWNER_TRAIT_EFFECT_PROJECTION",
                $"selected={selectedTraitIds.Length}; effective={effective?.ExpressedTraitIds.Count ?? 0}");
        }
        catch (Exception exception)
        {
            Check(false,
                "OWNER_TRAIT_EFFECT_PROJECTION",
                exception.GetType().Name + ":" + exception.Message);
        }

        DungeonRuntimeLifetimeScope gameplayScope =
            FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        try
        {
            ICharacterWorldSaveService characterSaves = gameplayScope?.Container?
                .Resolve<ICharacterWorldSaveService>();
            IGridSystemProvider grids = gameplayScope?.Container?
                .Resolve<IGridSystemProvider>();
            if (characterSaves == null || grids == null || !grids.TryGetGrid(out Grid grid))
            {
                Check(false,
                    "OWNER_TRAIT_CURRENT_SAVE",
                    $"scope={gameplayScope != null}; saves={characterSaves != null}; grids={grids != null}");
                return;
            }

            DungeonCharacterSaveData savedOwner = characterSaves.Capture(grid).actors
                .FirstOrDefault(actor => actor != null && actor.isOwner);
            DungeonCharacterSaveData reloaded = savedOwner != null
                ? JsonUtility.FromJson<DungeonCharacterSaveData>(
                    JsonUtility.ToJson(savedOwner))
                : null;
            DungeonGameRestoreReport validation = new();
            if (reloaded != null)
            {
                CharacterWorldSaveValidation.ValidateActor(
                    reloaded,
                    reloaded.persistentId,
                    validation);
            }

            Check(savedOwner != null
                    && reloaded != null
                    && validation.Success
                    && reloaded.growth.traitIds.SequenceEqual(actualOwnerTraitIds),
                "OWNER_TRAIT_CURRENT_SAVE",
                $"saved={savedOwner?.growth?.traitIds?.Count ?? -1}; "
                + $"reloaded={reloaded?.growth?.traitIds?.Count ?? -1}; "
                + $"errors={string.Join("|", validation.Errors)}");
        }
        catch (Exception exception)
        {
            Check(false,
                "OWNER_TRAIT_CURRENT_SAVE",
                exception.GetType().Name + ":" + exception.Message);
        }
    }

    private static TMP_Text FindMemberBodyText(int memberIndex)
    {
        string viewportName = $"StartPartyMemberBodyViewport_{memberIndex}";
        return Resources.FindObjectsOfTypeAll<TMP_Text>()
            .FirstOrDefault(text => text != null
                && text.gameObject.scene.IsValid()
                && text.gameObject.activeInHierarchy
                && string.Equals(
                    text.transform.parent?.name,
                    viewportName,
                    StringComparison.Ordinal));
    }

    private sealed class TraitCeilingProbeRandom : IRandomStream
    {
        private int drawCount;

        public ulong State => (ulong)drawCount;

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            }

            return drawCount++ == 0
                ? Math.Min(maxExclusive - 1, 90)
                : minInclusive;
        }

        public float NextFloat() => 0f;

        public bool Chance(float probability) => probability > 0f;

        public void Restore(ulong state)
        {
            if (state > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            drawCount = (int)state;
        }
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

public sealed class StartPartyOwnerTraitUpgradeFocusedRunner : MonoBehaviour
{
    private const float RuntimeReadyTimeoutSeconds = 45f;
    private const float PreparationReadyTimeoutSeconds = 180f;
    private const string PreparationRandomStreamId =
        "character:start-party-preparation";

    private readonly List<string> report = new();
    private readonly List<string> consoleErrors = new();
    private readonly List<string> consoleWarnings = new();
    private readonly List<Exception> cleanupFailures = new();
    private readonly List<FileIntegritySnapshot> realPersistenceBefore = new();

    private Exception verificationFailure;
    private List<DungeonSaveSectionEnvelope> originalWorld;
    private DungeonRuntimeLifetimeScope currentGameplayScope;
    private FixtureMetaProfileStore preparationProfileStore;
    private FixtureMetaProfileStore gameplayProfileStore;
    private FixtureDungeonGameSaveSlotService gameplaySlotService;
    private IDisposable preparationInstallation;
    private IDisposable gameplayInstallation;
    private DungeonAutosaveService originalAutosave;
    private MetaProfilePersistenceService originalMetaPersistence;
    private DungeonAutosaveService fixtureAutosave;
    private MetaProfilePersistenceService fixtureMetaPersistence;
    private string tempDirectory = string.Empty;
    private string tempProfilePath = string.Empty;
    private string stage = "not-started";
    private bool verificationCompleted;
    private bool committedWorldRoundTripped;
    private bool originalWorldRestored;
    private bool persistenceLeakChecked;
    private bool temporaryBaselineRun;
    private PreparedPartyExpectation expectedParty;

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory(Path.GetDirectoryName(
            StartPartyPreparationPlayModeVerifier
                .OwnerTraitUpgradeFocusedReportPath));
        tempDirectory = Path.Combine(
            "Temp",
            "Wim010StartingOwnerTrait",
            Guid.NewGuid().ToString("N"));
        tempProfilePath = Path.Combine(tempDirectory, "meta-profile.json");
        Directory.CreateDirectory(tempDirectory);
        report.Add("WIM-010 starting-owner-trait focused verification");
        report.Add("utc=" + DateTime.UtcNow.ToString("O"));
        Application.logMessageReceived += CaptureLog;

        yield return ExecuteGuarded(
            VerifyFocusedFlow(),
            exception => verificationFailure = exception);
        yield return ExecuteGuarded(
            Cleanup(),
            exception => cleanupFailures.Add(exception));

        bool passed = verificationCompleted
            && committedWorldRoundTripped
            && originalWorldRestored
            && persistenceLeakChecked
            && verificationFailure == null
            && cleanupFailures.Count == 0
            && consoleErrors.Count == 0
            && consoleWarnings.Count == 0;
        report.Insert(0, passed
            ? "WIM010_STARTING_OWNER_TRAIT_FOCUSED=PASS"
            : "WIM010_STARTING_OWNER_TRAIT_FOCUSED=FROZEN");
        report.Add("stage=" + stage);
        if (verificationFailure != null)
        {
            report.Add("verificationFailure=" + verificationFailure);
        }
        for (int index = 0; index < cleanupFailures.Count; index++)
        {
            report.Add("cleanupFailure[" + index + "]="
                + cleanupFailures[index]);
        }
        report.Add("consoleErrors=" + consoleErrors.Count);
        report.Add("consoleWarnings=" + consoleWarnings.Count);
        if (consoleErrors.Count > 0)
        {
            report.Add("ERRORS=" + string.Join(" || ", consoleErrors));
        }
        if (consoleWarnings.Count > 0)
        {
            report.Add("WARNINGS=" + string.Join(" || ", consoleWarnings));
        }
        report.Add("cleanup=" + (originalWorldRestored
            ? "whole-registry-restored"
            : "playmode-exit-discard"));
        report.Add("failures=" + (passed ? 0 : 1));

        Application.logMessageReceived -= CaptureLog;
        File.WriteAllLines(
            StartPartyPreparationPlayModeVerifier
                .OwnerTraitUpgradeFocusedReportPath,
            report);
        if (passed)
        {
            Debug.Log("WIM-010 starting-owner-trait focused verification passed. "
                + StartPartyPreparationPlayModeVerifier
                    .OwnerTraitUpgradeFocusedReportPath);
        }
        else
        {
            Debug.LogError(
                "WIM-010 starting-owner-trait focused verification failed. "
                + StartPartyPreparationPlayModeVerifier
                    .OwnerTraitUpgradeFocusedReportPath);
        }

        Destroy(gameObject);
        EditorApplication.ExitPlaymode();
    }

    private IEnumerator VerifyFocusedFlow()
    {
        stage = "resolve-original-gameplay";
        Require(string.Equals(
                SceneManager.GetActiveScene().name,
                DungeonSceneNavigator.GameplaySceneName,
                StringComparison.Ordinal),
            "WIM-010 focused verification must start in GameplayScene.");
        currentGameplayScope = FindActiveScope<DungeonRuntimeLifetimeScope>();
        Require(currentGameplayScope?.Container != null,
            "Current Gameplay DI scope is unavailable.");

        CaptureRealPersistenceIntegrity(currentGameplayScope);
        SuspendCurrentPersistence(currentGameplayScope);
        yield return EnsureValidBaselineOwner(currentGameplayScope);

        stage = "capture-original-world";
        IDungeonSaveSectionRegistry originalRegistry = currentGameplayScope
            .Container.Resolve<IDungeonSaveSectionRegistry>();
        float originalScaleBeforeFreeze = FreezeClockForSnapshot(
            currentGameplayScope,
            "original whole-registry capture");
        originalWorld = originalRegistry.CaptureAll();
        Require(originalWorld.Count > 0
                && originalWorld.Select(value => value.sectionId)
                    .Distinct(StringComparer.Ordinal).Count()
                    == originalWorld.Count,
            "Original whole-registry baseline is empty or contains duplicate sections.");
        report.Add("[PASS] ORIGINAL_WORLD_CAPTURE sections="
            + originalWorld.Count + "; temporaryRun=" + temporaryBaselineRun
            + "; scaleBeforeFreeze=" + originalScaleBeforeFreeze.ToString("0.###"));

        stage = "profile-contract-matrix";
        IMetaUpgradeDefinitionCatalog metaCatalog = currentGameplayScope
            .Container.Resolve<IMetaUpgradeDefinitionCatalog>();
        VerifyProfileContractMatrix(metaCatalog);

        WriteProfile(tempProfilePath, ProfileWithUpgrade(
            MetaUpgradeIds.StartingOwnerTraitCandidatePlusOne,
            1));
        preparationProfileStore = new FixtureMetaProfileStore(tempProfilePath);
        gameplayProfileStore = new FixtureMetaProfileStore(tempProfilePath);
        FocusedPreparationRandomStreamProvider preparationRandom = new();

        stage = "navigate-preparation";
        preparationInstallation = LifetimeScope.Enqueue(builder =>
        {
            builder.RegisterInstance(preparationProfileStore)
                .As<IMetaProfileStore>();
            builder.RegisterInstance(preparationRandom)
                .As<IRandomStreamProvider>();
        });
        IDungeonSceneNavigator originalNavigator = currentGameplayScope
            .Container.Resolve<IDungeonSceneNavigator>();
        Require(originalNavigator.StartNewPreparation(DungeonDifficulty.Normal),
            "Production navigator rejected the focused preparation transition.");

        DungeonPreparationLifetimeScope preparationScope = null;
        float preparationDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        while (Time.realtimeSinceStartup < preparationDeadline)
        {
            preparationScope = FindActiveScope<DungeonPreparationLifetimeScope>();
            if (preparationScope?.Container != null
                && string.Equals(
                    SceneManager.GetActiveScene().name,
                    DungeonSceneNavigator.PreparationSceneName,
                    StringComparison.Ordinal))
            {
                break;
            }
            yield return null;
        }
        Require(preparationScope?.Container != null,
            "StartPreparationScene did not expose its production DI scope.");
        preparationInstallation.Dispose();
        preparationInstallation = null;
        Require(ReferenceEquals(
                preparationScope.Container.Resolve<IMetaProfileStore>(),
                preparationProfileStore)
            && ReferenceEquals(
                preparationScope.Container.Resolve<IRandomStreamProvider>(),
                preparationRandom),
            "Preparation fixture store/random overrides were not authoritative.");

        yield return VerifyPreparationUiAndCaptureParty(preparationScope);
        Require(expectedParty != null,
            "Preparation expectations were not captured.");

        stage = "navigate-committed-gameplay";
        gameplaySlotService = new FixtureDungeonGameSaveSlotService();
        gameplayInstallation = LifetimeScope.Enqueue(builder =>
        {
            builder.RegisterInstance(gameplaySlotService)
                .As<IDungeonGameSaveSlotService>();
            builder.RegisterInstance(gameplayProfileStore)
                .As<IMetaProfileStore>();
        });
        Button start = FindActiveButton("PreparationStartRunButton");
        Require(start != null && start.interactable,
            "Production preparation Start button is not ready.");
        yield return Click(start);

        DungeonRuntimeLifetimeScope committedScope = null;
        float gameplayDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        while (Time.realtimeSinceStartup < gameplayDeadline)
        {
            committedScope = FindActiveScope<DungeonRuntimeLifetimeScope>();
            if (committedScope?.Container != null
                && string.Equals(
                    SceneManager.GetActiveScene().name,
                    DungeonSceneNavigator.GameplaySceneName,
                    StringComparison.Ordinal))
            {
                break;
            }
            yield return null;
        }
        Require(committedScope?.Container != null,
            "PreparedNewRun did not reach the production Gameplay scope.");
        gameplayInstallation.Dispose();
        gameplayInstallation = null;
        currentGameplayScope = committedScope;

        Require(ReferenceEquals(
                committedScope.Container.Resolve<IDungeonGameSaveSlotService>(),
                gameplaySlotService)
            && ReferenceEquals(
                committedScope.Container.Resolve<IMetaProfileStore>(),
                gameplayProfileStore),
            "Committed Gameplay did not retain both persistence overrides.");
        fixtureAutosave = committedScope.Container
            .Resolve<IDungeonSaveCommandService>() as DungeonAutosaveService;
        fixtureMetaPersistence = committedScope.Container
            .Resolve<MetaProfilePersistenceService>();
        Require(fixtureAutosave != null && fixtureMetaPersistence != null,
            "Committed Gameplay persistence entry points were not resolvable.");

        stage = "verify-committed-party";
        yield return WaitForCommittedOwner(committedScope, expectedParty.OwnerTraitIds);
        FreezeClockForSnapshot(committedScope, "committed party verification");
        VerifyCommittedParty(committedScope, expectedParty, "COMMITTED");
        Require(gameplaySlotService.DeletedSlotIds.SequenceEqual(
                new[]
                {
                    DungeonGameSaveSlotService.AutoSaveSlot,
                    DungeonGameSaveSlotService.QuickSaveSlot,
                    DungeonGameSaveSlotService.ManualSaveSlot
                },
                StringComparer.Ordinal)
            && gameplaySlotService.SaveCount == 0
            && gameplayProfileStore.LoadCount == 1
            && gameplayProfileStore.SaveCount == 0,
            "Fixture persistence calls differed from PreparedNewRun deletion/load only: "
            + "deletes=" + string.Join(",", gameplaySlotService.DeletedSlotIds)
            + "; slotSaves=" + gameplaySlotService.SaveCount
            + "; profileLoads=" + gameplayProfileStore.LoadCount
            + "; profileSaves=" + gameplayProfileStore.SaveCount + ".");
        report.Add("[PASS] PERSISTENCE_ISOLATION deletes=autosave,quicksave,manual; "
            + "slotSaves=0; profileLoads=1; profileSaves=0");

        stage = "roundtrip-committed-world";
        IDungeonSaveSectionRegistry committedRegistry = committedScope.Container
            .Resolve<IDungeonSaveSectionRegistry>();
        List<DungeonSaveSectionEnvelope> committedWorld =
            committedRegistry.CaptureAll();
        DungeonGameRestoreReport restoreReport = new();
        Require(committedRegistry.RestoreAll(committedWorld, restoreReport)
                && restoreReport.Success,
            "Committed whole-registry self-restore failed: "
            + string.Join(" | ", restoreReport.Errors));
        float committedRestoredScale = FreezeClockForSnapshot(
            committedScope,
            "committed whole-registry restore");
        yield return null;
        yield return null;
        Require(Mathf.Approximately(Time.timeScale, 0f),
            "Committed whole-registry comparison clock resumed after restore: "
            + Time.timeScale.ToString("0.###") + ".");
        VerifyCommittedParty(committedScope, expectedParty, "RESTORED_COMMITTED");
        List<DungeonSaveSectionEnvelope> committedAfter =
            committedRegistry.CaptureAll();
        Require(SaveEnvelopesEqual(
                committedWorld,
                committedAfter,
                allowMetaClockRounding: true),
            "Committed whole-registry self-restore changed non-clock payload bytes.");
        committedWorldRoundTripped = true;
        report.Add("[PASS] COMMITTED_REGISTRY_ROUNDTRIP sections="
            + committedWorld.Count + "; ownerTraits=5; staffTraits=4,4"
            + "; restoredScaleBeforeFreeze="
            + committedRestoredScale.ToString("0.###"));

        verificationCompleted = true;
        stage = "verification-complete";
    }

    private IEnumerator VerifyPreparationUiAndCaptureParty(
        DungeonPreparationLifetimeScope preparationScope)
    {
        stage = "prepare-through-production-ui";
        yield return null;
        Canvas.ForceUpdateCanvases();
        EventSystem eventSystem = FindActiveComponent<EventSystem>();
        IStartPartyPreparationService preparation = preparationScope.Container
            .Resolve<IStartPartyPreparationService>();
        Require(eventSystem != null && preparation != null,
            "Preparation UI EventSystem/service is unavailable.");

        CharacterSO ownerData = FindValidOwnerCandidate(preparationScope);
        Button ownerButton = FindActiveButton("OwnerCandidate_" + ownerData.id);
        Button nextButton = FindActiveButton("PreparationOwnerNextButton");
        Require(ownerButton != null && nextButton != null,
            "Selected valid owner or production Next button is missing.");
        yield return Click(ownerButton);
        nextButton = FindActiveButton("PreparationOwnerNextButton");
        yield return Click(nextButton);
        yield return null;

        Require(preparation.IsPreparing
                && preparationProfileStore.LoadCount == 1,
            "Production Begin did not consume the purchased profile exactly once: "
            + "preparing=" + preparation.IsPreparing
            + "; loads=" + preparationProfileStore.LoadCount + ".");
        StartPartyMemberPreparation owner = preparation.Members
            .SingleOrDefault(member => member != null && member.IsOwner);
        int[] ownerTraitIds = owner?.Progression?.GrowthState?.traitIds
            ?.ToArray() ?? Array.Empty<int>();
        Require(owner != null
                && owner.IsOwnerLocked
                && ownerTraitIds.Length == 5
                && ownerTraitIds.Distinct().Count() == 5
                && preparation.Roster
                    .Where(member => member != null && !member.IsOwner)
                    .All(member => member.Progression?.GrowthState?.traitIds
                        ?.Count == 4),
            "Actual preparation did not produce owner5/staff4: owner="
            + ownerTraitIds.Length + "; rosterStaff="
            + string.Join(",", preparation.Roster
                .Where(member => member != null && !member.IsOwner)
                .Select(member => member.Progression?.GrowthState?.traitIds
                    ?.Count ?? -1)) + ".");
        report.Add("[PASS] ACTUAL_PREPARATION_COUNTS owner=5; rosterStaff="
            + string.Join(",", preparation.Roster
                .Where(member => member != null && !member.IsOwner)
                .Select(member => member.Progression.GrowthState.traitIds.Count))
            + "; profileLoads=1");

        VerifyFactoryRoleCeilings(preparationScope, owner);

        StartPartyMemberPreparation staff = preparation.Members
            .First(member => member != null && !member.IsOwner);
        int staffRerollsBefore = staff.IdentityRerollsRemaining;
        int[] ownerBeforeStaffReroll = ownerTraitIds.ToArray();
        yield return Click(FindActiveButton(
            "PreparationRosterCard_" + staff.Index));
        yield return Click(FindActiveButton(
            "PreparationTab_" + staff.Index + "_Identity"));
        Button[] staffRerollButtons = FindActiveButtons(
            "PreparationIdentityRerollDice_" + staff.Index);
        Require(staffRerollButtons.Length == 1
                && staffRerollButtons[0].interactable,
            "Selected staff identity reroll control is missing or duplicated.");
        yield return Click(staffRerollButtons[0]);
        yield return null;
        owner = preparation.Members.Single(member => member.IsOwner);
        Require(owner.IsOwnerLocked
                && owner.Progression.GrowthState.traitIds.SequenceEqual(
                    ownerBeforeStaffReroll)
                && staff.IdentityRerollsRemaining == staffRerollsBefore - 1
                && staff.Progression.GrowthState.traitIds.Count == 4
                && staff.Progression.GrowthState.traitIds.Distinct().Count() == 4
                && preparationProfileStore.LoadCount == 1,
            "Staff reroll changed/requeried the locked owner contract: "
            + "owner=" + string.Join(",", owner.Progression.GrowthState.traitIds)
            + "; staffCount=" + staff.Progression.GrowthState.traitIds.Count
            + "; rerolls=" + staffRerollsBefore + "->"
            + staff.IdentityRerollsRemaining
            + "; profileLoads=" + preparationProfileStore.LoadCount + ".");
        report.Add("[PASS] OWNER_LOCK_AND_SINGLE_QUERY staffIdentityReroll="
            + staffRerollsBefore + "->" + staff.IdentityRerollsRemaining
            + "; ownerUnchanged=True; profileLoads=1");

        yield return WaitForPreparationReady(preparation);
        yield return VerifyOwnerTraitUi(owner, ownerTraitIds, eventSystem);
        StartPartyMemberPreparation[] selected = preparation.Members
            .OrderBy(member => member.PartySlot)
            .ToArray();
        owner = selected.Single(member => member.IsOwner);
        CharacterTraitSO[] ownerTraits = owner.Progression
            .ResolveSelectedTraits()
            .Where(trait => trait != null)
            .ToArray();
        Require(ownerTraits.Length == 5
                && selected.Where(member => !member.IsOwner)
                    .All(member => member.Progression.GrowthState.traitIds
                        .Count == 4)
                && preparationProfileStore.LoadCount == 1,
            "Prepared start readiness changed the owner/staff count or requeried meta.");
        expectedParty = new PreparedPartyExpectation(
            owner.Progression.GrowthState.traitIds.ToArray(),
            ownerTraits.Select(trait => trait.DefinitionId.Value).ToArray(),
            selected.Where(member => !member.IsOwner)
                .Select(member => member.Progression.GrowthState.traitIds
                    .ToArray())
                .ToArray(),
            owner.CharacterData.SpeciesTag);
        report.Add("[PASS] PREPARED_UI_READY ownerTraits="
            + string.Join(",", expectedParty.OwnerTraitIds)
            + "; staff=" + string.Join("|", expectedParty.StaffTraitIds
                .Select(ids => string.Join(",", ids)))
            + "; startInteractable=True");
    }

    private CharacterSO FindValidOwnerCandidate(
        DungeonPreparationLifetimeScope preparationScope)
    {
        IOwnerCandidateCatalog owners = preparationScope.Container
            .Resolve<IOwnerCandidateCatalog>();
        IRunCharacterCatalog characters = preparationScope.Container
            .Resolve<IRunCharacterCatalog>();
        IGameContentCatalog content = preparationScope.Container
            .Resolve<IGameContentCatalog>();
        ICharacterSkillSystemSettingsProvider settings = preparationScope
            .Container.Resolve<ICharacterSkillSystemSettingsProvider>();
        CharacterTraitSO[] traits = content.GetAll<CharacterTraitSO>()
            .Where(trait => trait != null)
            .OrderBy(trait => trait.id)
            .ToArray();
        foreach (CharacterSO candidate in owners.OwnerCandidates
                     .Where(value => value != null && value.IsOwnerCandidate)
                     .OrderBy(value => value.id))
        {
            CharacterSO staff = characters.Characters
                .Where(value => value != null
                    && value.characterType == CharacterType.Customer
                    && string.Equals(
                        value.SpeciesTag,
                        candidate.SpeciesTag,
                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(value => value.id)
                .FirstOrDefault();
            if (staff == null)
            {
                continue;
            }

            try
            {
                IReadOnlyList<int> ownerFive = CharacterTraitSelectionRules
                    .Select(
                        traits,
                        settings.Settings.traitConflicts,
                        new FocusedMaximumTraitRandomStream(),
                        candidate.SpeciesTag,
                        maximumCount: 5,
                        traitCountBonus: 1);
                IReadOnlyList<int> staffFour = CharacterTraitSelectionRules
                    .Select(
                        traits,
                        settings.Settings.traitConflicts,
                        new FocusedMaximumTraitRandomStream(),
                        staff.SpeciesTag,
                        maximumCount: 4);
                if (ownerFive.Count == 5 && staffFour.Count == 4)
                {
                    report.Add("[PASS] VALID_OWNER_SELECTION id=" + candidate.id
                        + "; species=" + candidate.SpeciesTag
                        + "; deterministicTraitTarget=5/4");
                    return candidate;
                }
            }
            catch (InvalidOperationException)
            {
                // Try the next authored owner; production Begin remains the
                // only authority that creates the actual prepared identities.
            }
        }

        throw new InvalidOperationException(
            "No authored owner/staff species can satisfy the real 5/4 trait rules.");
    }

    private IEnumerator VerifyOwnerTraitUi(
        StartPartyMemberPreparation owner,
        IReadOnlyList<int> expectedTraitIds,
        EventSystem eventSystem)
    {
        stage = "verify-owner-five-trait-ui";
        yield return Click(FindActiveButton(
            "PreparationRosterCard_" + owner.Index));
        yield return Click(FindActiveButton(
            "PreparationTab_" + owner.Index + "_Identity"));
        Canvas.ForceUpdateCanvases();
        yield return null;

        RectTransform[] chips = FindActiveRectsByPrefix("TraitChip_");
        Require(chips.Length == 5
                && FindActiveButtons("PreparationFullRerollDice_"
                    + owner.Index).Length == 0
                && FindActiveButtons("PreparationIdentityRerollDice_"
                    + owner.Index).Length == 0,
            "Owner identity UI did not expose exactly five chips with no reroll controls: "
            + "chips=" + chips.Length + ".");
        CharacterTraitSO[] expectedTraits = owner.Progression
            .ResolveSelectedTraits()
            .Where(trait => trait != null)
            .ToArray();
        Require(expectedTraits.Length == 5
                && expectedTraits.Select(trait => trait.id)
                    .SequenceEqual(expectedTraitIds),
            "Owner UI trait authority differs from prepared trait IDs.");

        foreach (CharacterTraitSO trait in expectedTraits)
        {
            RectTransform chip = FindSingleActiveTraitChip(trait.id);
            TMP_Text label = chip.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(value => value != null
                    && string.Equals(
                        value.name,
                        "TraitName",
                        StringComparison.Ordinal));
            Require(chip.gameObject.activeInHierarchy
                    && chip.anchorMin.y >= 0f
                    && chip.anchorMax.y <= 1f
                    && chip.anchorMax.y > chip.anchorMin.y
                    && label != null
                    && !string.IsNullOrEmpty(label.text)
                    && label.text.Contains(
                        trait.traitName,
                        StringComparison.Ordinal),
                "Owner trait chip is missing, clipped, or mislabeled: id="
                + trait.id + ".");

            PointerEventData pointer = new(eventSystem)
            {
                pointerEnter = chip.gameObject,
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    chip.TransformPoint(chip.rect.center))
            };
            bool entered = ExecuteEvents.Execute(
                chip.gameObject,
                pointer,
                ExecuteEvents.pointerEnterHandler);
            yield return null;
            GameObject tooltip = FindActiveGameObject("TraitStatTooltip");
            Require(entered
                    && tooltip != null
                    && tooltip.GetComponentsInChildren<TMP_Text>(true)
                        .Any(value => value != null
                            && !string.IsNullOrEmpty(value.text)
                            && value.text.Contains(
                            trait.traitName,
                            StringComparison.Ordinal)),
                "Owner trait tooltip did not render for trait id="
                + trait.id + ".");
            RectTransform exitChip = FindSingleActiveTraitChip(trait.id);
            pointer.pointerEnter = exitChip.gameObject;
            pointer.position = RectTransformUtility.WorldToScreenPoint(
                null,
                exitChip.TransformPoint(exitChip.rect.center));
            ExecuteEvents.Execute(
                exitChip.gameObject,
                pointer,
                ExecuteEvents.pointerExitHandler);
            yield return null;
        }

        RectTransform[] layoutChips = FindActiveRectsByPrefix("TraitChip_");
        string[] expectedChipNames = expectedTraits
            .Select(value => "TraitChip_" + value.id)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(layoutChips.Length == 5
                && layoutChips.Select(value => value.name)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(expectedChipNames),
            "Owner trait UI no longer exposes the exact five live chips after hover input.");
        float[][] rows = layoutChips
            .OrderByDescending(value => value.anchorMin.y)
            .Select(value => new[] { value.anchorMin.y, value.anchorMax.y })
            .ToArray();
        for (int index = 1; index < rows.Length; index++)
        {
            Require(rows[index - 1][0] >= rows[index][1],
                "Owner trait chip rows overlap at indexes "
                + (index - 1) + "/" + index + ".");
        }
        float[] bottoms = rows
            .Select(value => value[0])
            .ToArray();
        report.Add("[PASS] OWNER_TRAIT_UI chips=5; names=5; tooltips=5; "
            + "ownerRerollControls=0; rows="
            + string.Join(",", bottoms.Select(value => value.ToString("0.###"))));
    }

    private void VerifyFactoryRoleCeilings(
        DungeonPreparationLifetimeScope preparationScope,
        StartPartyMemberPreparation owner)
    {
        stage = "verify-runtime-profile-role-ceilings";
        ICharacterRuntimeProfileFactory profiles = preparationScope.Container
            .Resolve<ICharacterRuntimeProfileFactory>();
        CharacterTraitSO[] fiveTraits = owner.Progression
            .ResolveSelectedTraits()
            .Where(trait => trait != null)
            .ToArray();
        StartPartyMemberPreparation staff = preparationScope.Container
            .Resolve<IStartPartyPreparationService>().Members
            .First(member => !member.IsOwner);
        CharacterRuntimeProfile ownerProfile = profiles.Create(
            CharacterSpawnRequest.FromAuthoring(
                owner.CharacterData,
                fiveTraits));
        Exception staffFailure = null;
        try
        {
            profiles.Create(CharacterSpawnRequest.FromAuthoring(
                staff.CharacterData,
                fiveTraits));
        }
        catch (Exception exception)
        {
            staffFailure = exception;
        }
        Require(ownerProfile?.ExpressedTraitIds.Count == 5
                && staffFailure is InvalidOperationException,
            "Runtime profile role ceiling did not accept owner5/reject staff5: "
            + "owner=" + (ownerProfile?.ExpressedTraitIds.Count ?? -1)
            + "; staffFailure=" + (staffFailure?.GetType().Name ?? "none") + ".");
        report.Add("[PASS] RUNTIME_PROFILE_ROLE_CEILING owner5=accepted; "
            + "staff5=InvalidOperationException");
    }

    private IEnumerator WaitForPreparationReady(
        IStartPartyPreparationService preparation)
    {
        stage = "wait-natural-preparation-readiness";
        float deadline = Time.realtimeSinceStartup
            + PreparationReadyTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            Button start = FindActiveButton("PreparationStartRunButton");
            if (start != null
                && start.interactable
                && preparation.Members.Count == 3
                && preparation.Members.All(member =>
                    member != null && member.IsReadyToStart))
            {
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.25f);
        }

        throw new InvalidOperationException(
            "Production prepared-party skills were not ready before the focused timeout.");
    }

    private void VerifyProfileContractMatrix(
        IMetaUpgradeDefinitionCatalog catalog)
    {
        MetaUpgradeDefinition definition = catalog.Get(
            MetaUpgradeIds.StartingOwnerTraitCandidatePlusOne);
        Require(definition != null && definition.maxLevel == 1,
            "Starting-owner-trait upgrade definition is missing or not maxLevel 1.");

        if (File.Exists(tempProfilePath))
        {
            File.Delete(tempProfilePath);
        }
        VerifyProfileQueryCase(
            catalog,
            "missing",
            expectedSuccess: true,
            expectedBonus: 0,
            expectedLoads: 0);

        WriteProfile(tempProfilePath, ProfileWithUpgrade(
            MetaUpgradeIds.StartingOwnerTraitCandidatePlusOne,
            1,
            new DungeonStringIntSaveEntry
            {
                key = "fixture:unrelated-upgrade",
                value = 99
            }));
        VerifyProfileQueryCase(
            catalog,
            "purchased-one",
            expectedSuccess: true,
            expectedBonus: 1,
            expectedLoads: 1);

        WriteProfile(tempProfilePath, ProfileWithUpgrade(
            " " + MetaUpgradeIds.StartingOwnerTraitCandidatePlusOne,
            1));
        VerifyProfileQueryCase(
            catalog,
            "whitespace-key",
            expectedSuccess: false,
            expectedBonus: 0,
            expectedLoads: 1);

        WriteProfile(tempProfilePath, new DungeonMetaProfileData
        {
            upgradeLevels = new List<DungeonStringIntSaveEntry>
            {
                new()
                {
                    key = MetaUpgradeIds.StartingOwnerTraitCandidatePlusOne,
                    value = 1
                },
                new()
                {
                    key = MetaUpgradeIds.StartingOwnerTraitCandidatePlusOne,
                    value = 1
                }
            }
        });
        VerifyProfileQueryCase(
            catalog,
            "duplicate-key",
            expectedSuccess: false,
            expectedBonus: 0,
            expectedLoads: 1);

        WriteProfile(tempProfilePath, ProfileWithUpgrade(
            MetaUpgradeIds.StartingOwnerTraitCandidatePlusOne,
            2));
        VerifyProfileQueryCase(
            catalog,
            "level-two",
            expectedSuccess: false,
            expectedBonus: 0,
            expectedLoads: 1);
        report.Add("[PASS] PROFILE_ADAPTER_MATRIX missing=0; purchased1=1; "
            + "whitespace=false; duplicate=false; level2=false; realProfileWrites=0");
    }

    private void VerifyProfileQueryCase(
        IMetaUpgradeDefinitionCatalog catalog,
        string id,
        bool expectedSuccess,
        int expectedBonus,
        int expectedLoads)
    {
        FixtureMetaProfileStore store = new(tempProfilePath);
        ProfileStoreStartingOwnerTraitCountBonusQuery query = new(
            store,
            catalog);
        bool success = query.TryGetStartingOwnerTraitCountBonus(
            out int bonus,
            out string failureReason);
        Require(success == expectedSuccess
                && bonus == expectedBonus
                && store.LoadCount == expectedLoads
                && store.SaveCount == 0,
            "Profile adapter case '" + id + "' differed: success="
            + success + "/" + expectedSuccess + "; bonus=" + bonus + "/"
            + expectedBonus + "; loads=" + store.LoadCount + "/"
            + expectedLoads + "; failure=" + failureReason + ".");
    }

    private static DungeonMetaProfileData ProfileWithUpgrade(
        string key,
        int level,
        params DungeonStringIntSaveEntry[] additional)
    {
        List<DungeonStringIntSaveEntry> entries = new()
        {
            new DungeonStringIntSaveEntry
            {
                key = key,
                value = level
            }
        };
        entries.AddRange(additional ?? Array.Empty<DungeonStringIntSaveEntry>());
        return new DungeonMetaProfileData { upgradeLevels = entries };
    }

    private static void WriteProfile(
        string path,
        DungeonMetaProfileData profile)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(path, JsonUtility.ToJson(profile, true));
    }

    private void CaptureRealPersistenceIntegrity(
        DungeonRuntimeLifetimeScope scope)
    {
        stage = "capture-real-persistence-hashes";
        IMetaProfileStore profileStore = scope.Container
            .Resolve<IMetaProfileStore>();
        IDungeonSaveSlotCatalog slotCatalog = scope.Container
            .Resolve<IDungeonSaveSlotCatalog>();
        string[] paths =
        {
            profileStore.ProfilePath,
            slotCatalog.GetPath(DungeonGameSaveSlotService.AutoSaveSlot),
            slotCatalog.GetPath(DungeonGameSaveSlotService.QuickSaveSlot),
            slotCatalog.GetPath(DungeonGameSaveSlotService.ManualSaveSlot)
        };
        Require(paths.All(path => !string.IsNullOrWhiteSpace(path))
                && paths.Select(Path.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    == paths.Length,
            "Real profile/save-slot paths are missing or duplicated.");
        foreach (string path in paths)
        {
            FileIntegritySnapshot snapshot = FileIntegritySnapshot.Capture(path);
            realPersistenceBefore.Add(snapshot);
            report.Add("[INFO] REAL_PERSISTENCE_BEFORE path=" + snapshot.Path
                + "; exists=" + snapshot.Exists
                + "; length=" + snapshot.Length
                + "; sha256=" + snapshot.Sha256);
        }
    }

    private void SuspendCurrentPersistence(DungeonRuntimeLifetimeScope scope)
    {
        stage = "suspend-original-persistence";
        originalAutosave = scope.Container.Resolve<IDungeonSaveCommandService>()
            as DungeonAutosaveService;
        originalMetaPersistence = scope.Container
            .Resolve<MetaProfilePersistenceService>();
        Require(originalAutosave != null && originalMetaPersistence != null,
            "Original autosave/meta persistence entry points are unavailable.");
        originalAutosave.Dispose();
        originalMetaPersistence.Dispose();
        report.Add("[PASS] ORIGINAL_PERSISTENCE_SUSPENDED autosave=True; "
            + "metaProfile=True; beforeNavigation=True");
    }

    private IEnumerator EnsureValidBaselineOwner(
        DungeonRuntimeLifetimeScope scope)
    {
        stage = "ensure-valid-baseline-owner";
        IOwnerRunManagerProvider providers = scope.Container
            .Resolve<IOwnerRunManagerProvider>();
        Require(providers.TryGetManager(out OwnerRunManager manager)
                && manager != null,
            "Original Gameplay owner manager is unavailable.");
        float deadline = Time.realtimeSinceStartup + RuntimeReadyTimeoutSeconds;
        while (manager.CurrentOwnerActor == null
               && (manager.OwnerCandidates == null
                   || manager.OwnerCandidates.Count == 0)
               && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            providers.TryGetManager(out manager);
        }

        if (manager.CurrentOwnerActor == null)
        {
            Require(manager.OwnerCandidates?.Count > 0,
                "No active owner or production owner candidate exists for baseline bootstrap.");
            temporaryBaselineRun = true;
            string bootstrap = StartPartyPreparationPlayModeVerifier
                .RunFastCommitForDebug();
            report.Add("[INFO] TEMPORARY_BASELINE_BOOTSTRAP " + bootstrap);
            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }
            providers.TryGetManager(out manager);
        }

        Require(manager?.CurrentOwnerActor != null,
            "A valid owner was not established before whole-registry capture.");
        report.Add("[PASS] BASELINE_OWNER id="
            + manager.CurrentOwnerActor.Identity?.PersistentId
            + "; temporaryRun=" + temporaryBaselineRun
            + "; exitIntent=PlayModeDiscard");
    }

    private IEnumerator WaitForCommittedOwner(
        DungeonRuntimeLifetimeScope scope,
        IReadOnlyList<int> expectedOwnerTraits)
    {
        IOwnerRunManagerProvider providers = scope.Container
            .Resolve<IOwnerRunManagerProvider>();
        float deadline = Time.realtimeSinceStartup + RuntimeReadyTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (providers.TryGetManager(out OwnerRunManager manager)
                && manager?.CurrentOwnerActor?.Progression?.GrowthState
                    ?.traitIds != null
                && manager.CurrentOwnerActor.Progression.GrowthState.traitIds
                    .SequenceEqual(expectedOwnerTraits))
            {
                yield break;
            }
            if (!string.Equals(
                    SceneManager.GetActiveScene().name,
                    DungeonSceneNavigator.GameplaySceneName,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "PreparedNewRun left Gameplay before applying the expected owner.");
            }
            yield return null;
        }

        throw new InvalidOperationException(
            "PreparedNewRun did not apply the expected owner traits before timeout.");
    }

    private void VerifyCommittedParty(
        DungeonRuntimeLifetimeScope scope,
        PreparedPartyExpectation expectation,
        string id)
    {
        IOwnerRunManagerProvider providers = scope.Container
            .Resolve<IOwnerRunManagerProvider>();
        ICharacterWorldQuery world = scope.Container
            .Resolve<ICharacterWorldQuery>();
        Require(providers.TryGetManager(out OwnerRunManager manager)
                && manager?.CurrentOwnerActor != null,
            id + " owner manager has no current owner.");
        CharacterActor owner = manager.CurrentOwnerActor;
        int[] ownerTraits = owner.Progression?.GrowthState?.traitIds
            ?.ToArray() ?? Array.Empty<int>();
        CharacterRuntimeProfile effective = owner.Progression?
            .GetEffectiveRuntimeProfile();
        string[] selectedDefinitions = owner.Progression?
            .ResolveSelectedTraits()
            .Where(trait => trait != null)
            .Select(trait => trait.DefinitionId.Value)
            .ToArray() ?? Array.Empty<string>();
        CharacterActor[] registered = (world.Characters
                ?? Array.Empty<CharacterActor>())
            .Where(actor => actor != null)
            .ToArray();
        CharacterActor[] staff = registered
            .Where(actor => actor != owner
                && actor.Identity != null
                && actor.Identity.PersistentId.StartsWith(
                    "character:staff:",
                    StringComparison.Ordinal))
            .ToArray();
        string[] expectedStaff = expectation.StaffTraitIds
            .Select(CanonicalTraitIds)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actualStaff = staff
            .Select(actor => CanonicalTraitIds(
                actor.Progression?.GrowthState?.traitIds
                    ?? new List<int>()))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] duplicateIds = registered
            .Where(actor => !string.IsNullOrWhiteSpace(
                actor.Identity?.PersistentId))
            .GroupBy(
                actor => actor.Identity.PersistentId,
                StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Require(ownerTraits.SequenceEqual(expectation.OwnerTraitIds)
                && ownerTraits.Length == 5
                && ownerTraits.Distinct().Count() == 5
                && string.Equals(
                    owner.SpeciesTag,
                    expectation.OwnerSpecies,
                    StringComparison.OrdinalIgnoreCase)
                && selectedDefinitions.SequenceEqual(
                    expectation.OwnerTraitDefinitionIds,
                    StringComparer.Ordinal)
                && effective != null
                && effective.ExpressedTraitIds.SequenceEqual(
                    expectation.OwnerTraitDefinitionIds,
                    StringComparer.Ordinal)
                && staff.Length == 2
                && staff.All(actor =>
                    actor.Progression?.GrowthState?.traitIds?.Count == 4
                    && actor.Progression.GrowthState.traitIds
                        .Distinct().Count() == 4)
                && actualStaff.SequenceEqual(
                    expectedStaff,
                    StringComparer.Ordinal)
                && duplicateIds.Length == 0,
            id + " party/effect/registry mismatch: owner="
            + string.Join(",", ownerTraits)
            + "; selected=" + string.Join(",", selectedDefinitions)
            + "; effective="
            + string.Join(",", effective?.ExpressedTraitIds
                ?? Array.Empty<string>())
            + "; staff=" + string.Join("|", actualStaff)
            + "; duplicates=" + string.Join(",", duplicateIds) + ".");
        report.Add("[PASS] " + id + " ownerTraits=5; staffTraits=4,4; "
            + "effects=5; registryIdentities=" + registered.Length
            + "; duplicateIds=0");
    }

    private static string CanonicalTraitIds(IEnumerable<int> values) =>
        string.Join(",", values ?? Array.Empty<int>());

    private static float FreezeClockForSnapshot(
        DungeonRuntimeLifetimeScope scope,
        string context)
    {
        Require(scope?.Container != null,
            "Gameplay scope is unavailable while freezing " + context + ".");
        IGameTimeScaleController timeScale = scope.Container
            .Resolve<IGameTimeScaleController>();
        float scaleBeforeFreeze = timeScale.Scale;
        timeScale.Scale = 0f;
        Require(Mathf.Approximately(timeScale.Scale, 0f)
                && Mathf.Approximately(Time.timeScale, 0f),
            "Gameplay clock did not freeze for " + context + ": controller="
            + timeScale.Scale.ToString("0.###") + "; unity="
            + Time.timeScale.ToString("0.###") + ".");
        return scaleBeforeFreeze;
    }

    private IEnumerator Cleanup()
    {
        stage = "cleanup";
        DisposeInstallation(ref preparationInstallation, "preparation");
        DisposeInstallation(ref gameplayInstallation, "gameplay");
        TryDisposeFixturePersistence();

        DungeonRuntimeLifetimeScope cleanupScope =
            FindActiveScope<DungeonRuntimeLifetimeScope>();
        if (originalWorld != null
            && cleanupScope?.Container != null
            && string.Equals(
                SceneManager.GetActiveScene().name,
                DungeonSceneNavigator.GameplaySceneName,
                StringComparison.Ordinal))
        {
            IDungeonSaveSectionRegistry registry = null;
            bool restoredForInspection = false;
            float restoredScaleBeforeFreeze = 0f;
            try
            {
                registry = cleanupScope.Container
                    .Resolve<IDungeonSaveSectionRegistry>();
                DungeonGameRestoreReport cleanupReport = new();
                if (!registry.RestoreAll(originalWorld, cleanupReport)
                    || !cleanupReport.Success)
                {
                    throw new InvalidOperationException(
                        "Original whole-registry cleanup restore failed: "
                        + string.Join(" | ", cleanupReport.Errors));
                }
                restoredScaleBeforeFreeze = FreezeClockForSnapshot(
                    cleanupScope,
                    "original whole-registry cleanup restore");
                restoredForInspection = true;
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }

            if (restoredForInspection)
            {
                yield return null;
                yield return null;
                try
                {
                    Require(Mathf.Approximately(Time.timeScale, 0f),
                        "Original-world comparison clock resumed after restore: "
                        + Time.timeScale.ToString("0.###") + ".");
                    List<DungeonSaveSectionEnvelope> restored =
                        registry.CaptureAll();
                    bool byteExact = SaveEnvelopesEqual(originalWorld, restored);
                    bool metaClockOnly = !byteExact && SaveEnvelopesEqual(
                        originalWorld,
                        restored,
                        allowMetaClockRounding: true);
                    if (!byteExact && !metaClockOnly)
                    {
                        report.Add("cleanupDifferences="
                            + DescribeEnvelopeDifferences(originalWorld, restored));
                        throw new InvalidOperationException(
                            "Original whole-registry cleanup changed non-clock payload bytes.");
                    }
                    originalWorldRestored = true;
                    report.Add("[PASS] ORIGINAL_WORLD_RESTORE sections="
                        + restored.Count + "; equivalence="
                        + (byteExact
                            ? "byte-exact"
                            : "exact-except-meta-elapsed-float-rounding-under-0.00001s")
                        + "; restoredScaleBeforeFreeze="
                        + restoredScaleBeforeFreeze.ToString("0.###"));
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(exception);
                }
            }
        }
        else if (originalWorld != null)
        {
            report.Add("[INFO] ORIGINAL_WORLD_RESTORE unavailableScene="
                + SceneManager.GetActiveScene().name
                + "; safety=PlayModeExitDiscardsTransientWorld");
        }

        try
        {
            if (gameplaySlotService != null
                && gameplaySlotService.SaveCount != 0)
            {
                throw new InvalidOperationException(
                    "Fixture slot service observed an unexpected save: "
                    + gameplaySlotService.SaveCount + ".");
            }
            if (gameplayProfileStore != null
                && gameplayProfileStore.SaveCount != 0)
            {
                throw new InvalidOperationException(
                    "Fixture meta store observed an unexpected save: "
                    + gameplayProfileStore.SaveCount + ".");
            }
            report.Add("[PASS] FIXTURE_PERSISTENCE_CLEANUP slotSaves=0; "
                + "profileSaves=0; subscriptions=disposed");
        }
        catch (Exception exception)
        {
            cleanupFailures.Add(exception);
        }

        CheckRealPersistenceIntegrity();
        try
        {
            string expectedRoot = Path.GetFullPath(Path.Combine(
                "Temp",
                "Wim010StartingOwnerTrait"));
            string actual = Path.GetFullPath(tempDirectory);
            Require(actual.StartsWith(
                    expectedRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase),
                "Focused temporary directory escaped its expected root.");
            if (Directory.Exists(actual))
            {
                Directory.Delete(actual, recursive: true);
            }
            report.Add("[PASS] TEMP_PROFILE_CLEANUP removed=True");
        }
        catch (Exception exception)
        {
            cleanupFailures.Add(exception);
        }

        stage = cleanupFailures.Count == 0
            ? "terminal-cleanup-complete"
            : "terminal-cleanup-failed";
    }

    private void TryDisposeFixturePersistence()
    {
        try
        {
            fixtureAutosave?.Dispose();
        }
        catch (Exception exception)
        {
            cleanupFailures.Add(new InvalidOperationException(
                "Fixture autosave dispose failed.",
                exception));
        }
        try
        {
            fixtureMetaPersistence?.Dispose();
        }
        catch (Exception exception)
        {
            cleanupFailures.Add(new InvalidOperationException(
                "Fixture meta persistence dispose failed.",
                exception));
        }
    }

    private void DisposeInstallation(ref IDisposable installation, string id)
    {
        if (installation == null)
        {
            return;
        }
        try
        {
            installation.Dispose();
        }
        catch (Exception exception)
        {
            cleanupFailures.Add(new InvalidOperationException(
                id + " fixture installation disposal failed.",
                exception));
        }
        finally
        {
            installation = null;
        }
    }

    private void CheckRealPersistenceIntegrity()
    {
        try
        {
            List<string> differences = new();
            foreach (FileIntegritySnapshot before in realPersistenceBefore)
            {
                FileIntegritySnapshot after = FileIntegritySnapshot.Capture(
                    before.Path);
                bool same = before.ContentEquals(after);
                report.Add("[INFO] REAL_PERSISTENCE_AFTER path=" + after.Path
                    + "; exists=" + after.Exists
                    + "; length=" + after.Length
                    + "; sha256=" + after.Sha256
                    + "; unchanged=" + same);
                if (!same)
                {
                    differences.Add(before.Path);
                }
            }
            if (differences.Count > 0)
            {
                throw new InvalidOperationException(
                    "Real profile/save bytes changed; fixture will not rewrite them: "
                    + string.Join(",", differences));
            }
            persistenceLeakChecked = true;
            report.Add("[PASS] REAL_PERSISTENCE_LEAK_GATE unchanged="
                + realPersistenceBefore.Count + "/"
                + realPersistenceBefore.Count);
        }
        catch (Exception exception)
        {
            cleanupFailures.Add(exception);
        }
    }

    private static bool SaveEnvelopesEqual(
        IReadOnlyList<DungeonSaveSectionEnvelope> left,
        IReadOnlyList<DungeonSaveSectionEnvelope> right,
        bool allowMetaClockRounding = false)
    {
        if (left == null || right == null || left.Count != right.Count)
        {
            return false;
        }
        for (int index = 0; index < left.Count; index++)
        {
            DungeonSaveSectionEnvelope a = left[index];
            DungeonSaveSectionEnvelope b = right[index];
            if (!string.Equals(a.sectionId, b.sectionId, StringComparison.Ordinal)
                || a.sectionVersion != b.sectionVersion
                || a.restorePhase != b.restorePhase
                || a.optional != b.optional
                || (!string.Equals(
                    a.payloadJson,
                    b.payloadJson,
                    StringComparison.Ordinal)
                    && !(allowMetaClockRounding
                        && string.Equals(
                            a.sectionId,
                            MetaProgressionSaveSection.Id,
                            StringComparison.Ordinal)
                        && MetaClockRoundingOnly(
                            a.payloadJson,
                            b.payloadJson))))
            {
                return false;
            }
        }
        return true;
    }

    private static bool MetaClockRoundingOnly(string before, string after)
    {
        DungeonMetaProgressionSaveData left = JsonUtility
            .FromJson<DungeonMetaProgressionSaveData>(before);
        DungeonMetaProgressionSaveData right = JsonUtility
            .FromJson<DungeonMetaProgressionSaveData>(after);
        if (left?.runProgress == null
            || right?.runProgress == null
            || !(Math.Abs(
                (double)left.runProgress.elapsedSeconds
                - right.runProgress.elapsedSeconds) < 0.00001d))
        {
            return false;
        }
        right.runProgress.elapsedSeconds = left.runProgress.elapsedSeconds;
        return string.Equals(
            JsonUtility.ToJson(left),
            JsonUtility.ToJson(right),
            StringComparison.Ordinal);
    }

    private static string DescribeEnvelopeDifferences(
        IReadOnlyList<DungeonSaveSectionEnvelope> before,
        IReadOnlyList<DungeonSaveSectionEnvelope> after)
    {
        Dictionary<string, DungeonSaveSectionEnvelope> afterById =
            (after ?? Array.Empty<DungeonSaveSectionEnvelope>())
            .Where(value => value != null)
            .ToDictionary(value => value.sectionId, StringComparer.Ordinal);
        return string.Join(",", (before
                ?? Array.Empty<DungeonSaveSectionEnvelope>())
            .Where(value => value != null)
            .Where(value => !afterById.TryGetValue(
                    value.sectionId,
                    out DungeonSaveSectionEnvelope current)
                || value.sectionVersion != current.sectionVersion
                || value.restorePhase != current.restorePhase
                || value.optional != current.optional
                || !string.Equals(
                    value.payloadJson,
                    current.payloadJson,
                    StringComparison.Ordinal))
            .Select(value => value.sectionId));
    }

    private static IEnumerator ExecuteGuarded(
        IEnumerator root,
        Action<Exception> onFailure)
    {
        Stack<IEnumerator> stack = new();
        stack.Push(root ?? throw new ArgumentNullException(nameof(root)));
        while (stack.Count > 0)
        {
            IEnumerator currentRoutine = stack.Peek();
            object yielded = null;
            bool moved;
            try
            {
                moved = currentRoutine.MoveNext();
                if (moved)
                {
                    yielded = currentRoutine.Current;
                }
            }
            catch (Exception exception)
            {
                onFailure?.Invoke(exception);
                yield break;
            }

            if (!moved)
            {
                (currentRoutine as IDisposable)?.Dispose();
                stack.Pop();
                continue;
            }
            if (yielded is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            yield return yielded;
        }
    }

    private IEnumerator Click(Button button)
    {
        Require(button != null
                && button.gameObject.activeInHierarchy
                && button.interactable,
            "Production UI button is missing or not interactable.");
        EventSystem eventSystem = FindActiveComponent<EventSystem>();
        Require(eventSystem != null,
            "Production UI EventSystem is unavailable.");
        RectTransform rect = button.transform as RectTransform;
        PointerEventData pointer = new(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(
                null,
                rect != null
                    ? rect.TransformPoint(rect.rect.center)
                    : button.transform.position),
            pointerPress = button.gameObject,
            pointerEnter = button.gameObject
        };
        bool handled = ExecuteEvents.Execute(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerClickHandler);
        Require(handled,
            "Production pointer-click route did not handle button '"
            + button.name + "'.");
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
    }

    private static T FindActiveScope<T>() where T : LifetimeScope =>
        Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(value => value != null
                && value.gameObject.scene.IsValid()
                && value.gameObject.activeInHierarchy
                && value.Container != null);

    private static T FindActiveComponent<T>() where T : Component =>
        Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(value => value != null
                && value.gameObject.scene.IsValid()
                && value.gameObject.activeInHierarchy);

    private static Button FindActiveButton(string name) =>
        FindActiveButtons(name).FirstOrDefault();

    private static Button[] FindActiveButtons(string name) =>
        Resources.FindObjectsOfTypeAll<Button>()
            .Where(value => value != null
                && value.gameObject.scene.IsValid()
                && value.gameObject.activeInHierarchy
                && string.Equals(value.name, name, StringComparison.Ordinal))
            .ToArray();

    private static RectTransform[] FindActiveRectsByPrefix(string prefix) =>
        Resources.FindObjectsOfTypeAll<RectTransform>()
            .Where(value => value != null
                && value.gameObject.scene.IsValid()
                && value.gameObject.activeInHierarchy
                && value.name.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();

    private static RectTransform FindSingleActiveTraitChip(int traitId)
    {
        string expectedName = "TraitChip_" + traitId;
        RectTransform[] matches = FindActiveRectsByPrefix("TraitChip_")
            .Where(value => string.Equals(
                value.name,
                expectedName,
                StringComparison.Ordinal))
            .ToArray();
        Require(matches.Length == 1
                && matches[0] != null
                && matches[0].gameObject.activeInHierarchy,
            "Owner trait UI requires one live chip named "
            + expectedName + "; actual=" + matches.Length + ".");
        return matches[0];
    }

    private static GameObject FindActiveGameObject(string name) =>
        Resources.FindObjectsOfTypeAll<Transform>()
            .Where(value => value != null
                && value.gameObject.scene.IsValid()
                && value.gameObject.activeInHierarchy)
            .Select(value => value.gameObject)
            .FirstOrDefault(value => string.Equals(
                value.name,
                name,
                StringComparison.Ordinal));

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            consoleWarnings.Add(condition);
        }
        else if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            consoleErrors.Add(condition + "\n" + stackTrace);
        }
    }

    private sealed class PreparedPartyExpectation
    {
        public PreparedPartyExpectation(
            int[] ownerTraitIds,
            string[] ownerTraitDefinitionIds,
            int[][] staffTraitIds,
            string ownerSpecies)
        {
            OwnerTraitIds = ownerTraitIds ?? Array.Empty<int>();
            OwnerTraitDefinitionIds = ownerTraitDefinitionIds
                ?? Array.Empty<string>();
            StaffTraitIds = staffTraitIds ?? Array.Empty<int[]>();
            OwnerSpecies = ownerSpecies ?? string.Empty;
        }

        public int[] OwnerTraitIds { get; }
        public string[] OwnerTraitDefinitionIds { get; }
        public int[][] StaffTraitIds { get; }
        public string OwnerSpecies { get; }
    }

    private sealed class FixtureMetaProfileStore : IMetaProfileStore
    {
        public FixtureMetaProfileStore(string profilePath)
        {
            ProfilePath = string.IsNullOrWhiteSpace(profilePath)
                ? throw new ArgumentException(
                    "Fixture profile path is required.",
                    nameof(profilePath))
                : Path.GetFullPath(profilePath);
        }

        public string ProfilePath { get; }
        public int LoadCount { get; private set; }
        public int SaveCount { get; private set; }

        public bool TryLoad(out DungeonMetaProfileData profile)
        {
            LoadCount++;
            profile = null;
            if (!File.Exists(ProfilePath))
            {
                return false;
            }
            try
            {
                profile = JsonUtility.FromJson<DungeonMetaProfileData>(
                    File.ReadAllText(ProfilePath));
                return profile != null
                    && profile.version == DungeonMetaProfileData.CurrentVersion;
            }
            catch
            {
                profile = null;
                return false;
            }
        }

        public void Save(MetaProgressionState state)
        {
            _ = state ?? throw new ArgumentNullException(nameof(state));
            SaveCount++;
        }
    }

    private sealed class FixtureDungeonGameSaveSlotService :
        IDungeonGameSaveSlotService
    {
        private readonly List<string> deletedSlotIds = new();

        public IReadOnlyList<string> DeletedSlotIds => deletedSlotIds;
        public int SaveCount { get; private set; }

        public string Save(string slotId, bool prettyPrint = false)
        {
            SaveCount++;
            return "fixture://" + (slotId ?? string.Empty);
        }

        public bool TryLoad(
            string slotId,
            out DungeonGameRestoreReport restoreReport)
        {
            restoreReport = new DungeonGameRestoreReport();
            restoreReport.AddError(
                "Fixture save slot has no payload: " + slotId);
            return false;
        }

        public bool HasSave(string slotId) => false;

        public IReadOnlyList<DungeonSaveSlotInfo> GetSlots() =>
            Array.Empty<DungeonSaveSlotInfo>();

        public bool Delete(string slotId)
        {
            deletedSlotIds.Add(slotId ?? string.Empty);
            return false;
        }
    }

    private sealed class FocusedPreparationRandomStreamProvider :
        IRandomStreamProvider
    {
        private readonly RandomStreamProvider inner = new(10010);
        private readonly Dictionary<string, IRandomStream> wrapped =
            new(StringComparer.Ordinal);

        public int RootSeed => inner.RootSeed;

        public IRandomStream Get(string streamId)
        {
            if (!string.Equals(
                    streamId,
                    PreparationRandomStreamId,
                    StringComparison.Ordinal))
            {
                return inner.Get(streamId);
            }
            if (!wrapped.TryGetValue(streamId, out IRandomStream stream))
            {
                stream = new FocusedMaximumTraitRandomStream(
                    inner.Get(streamId));
                wrapped.Add(streamId, stream);
            }
            return stream;
        }

        public void Reseed(int rootSeed) => inner.Reseed(rootSeed);

        public IReadOnlyList<RandomStreamStateSnapshot> CaptureStates() =>
            inner.CaptureStates();

        public RandomStreamRestoreCandidate BuildRestoreStates(
            int rootSeed,
            IEnumerable<RandomStreamStateSnapshot> snapshots) =>
            inner.BuildRestoreStates(rootSeed, snapshots);

        public void RestoreStates(RandomStreamRestoreCandidate candidate) =>
            inner.RestoreStates(candidate);

        public void RestoreStates(
            int rootSeed,
            IEnumerable<RandomStreamStateSnapshot> snapshots) =>
            inner.RestoreStates(rootSeed, snapshots);
    }

    private sealed class FocusedMaximumTraitRandomStream : IRandomStream
    {
        private readonly IRandomStream inner;
        private ulong standaloneState;

        public FocusedMaximumTraitRandomStream()
        {
        }

        public FocusedMaximumTraitRandomStream(IRandomStream inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public ulong State => inner?.State ?? standaloneState;

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            }
            int consumed = inner != null
                ? inner.NextInt(minInclusive, maxExclusive)
                : ConsumeStandalone(minInclusive);
            return minInclusive == 0 && maxExclusive == 100
                ? 99
                : consumed;
        }

        public float NextFloat()
        {
            if (inner != null)
            {
                return inner.NextFloat();
            }
            standaloneState++;
            return 0f;
        }

        public bool Chance(float probability)
        {
            if (inner != null)
            {
                return inner.Chance(probability);
            }
            standaloneState++;
            return probability > 0f;
        }

        public void Restore(ulong state)
        {
            if (inner != null)
            {
                inner.Restore(state);
            }
            else
            {
                standaloneState = state;
            }
        }

        private int ConsumeStandalone(int minInclusive)
        {
            standaloneState++;
            return minInclusive;
        }
    }

    private sealed class FileIntegritySnapshot
    {
        private FileIntegritySnapshot(
            string path,
            bool exists,
            long length,
            string sha256)
        {
            Path = path;
            Exists = exists;
            Length = length;
            Sha256 = sha256;
        }

        public string Path { get; }
        public bool Exists { get; }
        public long Length { get; }
        public string Sha256 { get; }

        public static FileIntegritySnapshot Capture(string path)
        {
            string fullPath = System.IO.Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return new FileIntegritySnapshot(
                    fullPath,
                    exists: false,
                    length: 0,
                    sha256: "MISSING");
            }
            using FileStream stream = File.Open(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using SHA256 sha = SHA256.Create();
            string digest = BitConverter.ToString(sha.ComputeHash(stream))
                .Replace("-", string.Empty);
            return new FileIntegritySnapshot(
                fullPath,
                exists: true,
                length: stream.Length,
                sha256: digest);
        }

        public bool ContentEquals(FileIntegritySnapshot other) =>
            other != null
            && Exists == other.Exists
            && Length == other.Length
            && string.Equals(
                Sha256,
                other.Sha256,
                StringComparison.OrdinalIgnoreCase);
    }
}
#endif
