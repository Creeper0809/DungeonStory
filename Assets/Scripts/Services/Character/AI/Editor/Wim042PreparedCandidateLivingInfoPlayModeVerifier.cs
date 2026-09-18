#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using DungeonStory.Foundation;

/// <summary>
/// Focused WIM042 coverage for the production OwnerSelectionPanel. The probe
/// starts preparation through the real Button/EventSystem path and completes
/// the existing prepared-start commit. It calls no save API and exits PlayMode
/// after the live-state read so the fixture is discarded.
/// </summary>
public static class Wim042PreparedCandidateLivingInfoPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/wim-042-prepared-candidate-living-information-playmode-report.txt";

    [MenuItem("DungeonStory/Debug/QA/Run WIM042 Prepared Candidate Living Information")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("WIM042 verification requires PlayMode in the gameplay scene.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<Wim042PreparedCandidateLivingInfoRunner>() != null)
        {
            Debug.LogWarning("WIM042 verification is already running.");
            return;
        }

        new GameObject("WIM042 Prepared Candidate Living Information Runner")
            .AddComponent<Wim042PreparedCandidateLivingInfoRunner>();
    }
}

public sealed class Wim042PreparedCandidateLivingInfoRunner : MonoBehaviour
{
    private const string PreparationRandomStreamId = "character:start-party-preparation";
    private const float RuntimeReadyTimeoutSeconds = 10f;

    private readonly List<string> results = new List<string>();
    private readonly List<string> failures = new List<string>();
    private IStartPartyPreparationService preparation;
    private OwnerSelectionPanel panel;
    private float originalTimeScale;
    private bool runtimeReady;

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory(Path.GetDirectoryName(
            Wim042PreparedCandidateLivingInfoPlayModeVerifier.ReportPath) ?? "Artifacts/QA");
        originalTimeScale = Time.timeScale;
        yield return RunGuarded();

        Time.timeScale = originalTimeScale;
        try
        {
            if (runtimeReady)
            {
                preparation?.Cancel();
                panel?.RefreshVisibility();
            }
        }
        catch (Exception exception)
        {
            failures.Add("CLEANUP: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            Finish();
            Destroy(gameObject);
            EditorApplication.ExitPlaymode();
        }
    }

    private IEnumerator RunGuarded()
    {
        Stack<IEnumerator> stack = new();
        stack.Push(Run());
        while (stack.Count > 0)
        {
            object current;
            try
            {
                IEnumerator routine = stack.Peek();
                if (!routine.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                current = routine.Current;
            }
            catch (Exception exception)
            {
                failures.Add("UNHANDLED: " + exception);
                Debug.LogException(exception);
                yield break;
            }

            if (current is IEnumerator nested)
            {
                stack.Push(nested);
            }
            else
            {
                yield return current;
            }
        }
    }

    private IEnumerator Run()
    {
            float deadline = Time.realtimeSinceStartup + RuntimeReadyTimeoutSeconds;
            DungeonRuntimeLifetimeScope scope = null;
            EventSystem eventSystem = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                scope = UnityEngine.Object.FindFirstObjectByType<
                    DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include);
                panel = UnityEngine.Object.FindFirstObjectByType<OwnerSelectionPanel>(
                    FindObjectsInactive.Include);
                eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>(
                    FindObjectsInactive.Include);
                if (scope?.Container != null && panel != null && eventSystem != null)
                {
                    // VContainer has built the scope; let its injected scene
                    // behaviours complete their Start pass before touching the panel.
                    yield return null;
                    scope = UnityEngine.Object.FindFirstObjectByType<
                        DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include);
                    panel = UnityEngine.Object.FindFirstObjectByType<OwnerSelectionPanel>(
                        FindObjectsInactive.Include);
                    eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>(
                        FindObjectsInactive.Include);
                    if (scope?.Container != null && panel != null && eventSystem != null)
                    {
                        break;
                    }
                }

                yield return null;
            }

            Check(scope?.Container != null && panel != null && eventSystem != null,
                "RUNTIME_READY",
                $"scope={scope != null}; container={scope?.Container != null}; "
                + $"panel={panel != null}; eventSystem={eventSystem != null}");
            if (scope?.Container == null || panel == null || eventSystem == null)
            {
                yield break;
            }

            preparation = scope.Container.Resolve<IStartPartyPreparationService>();
            ICharacterNeedDefinitionCatalog needs = scope.Container
                .Resolve<ICharacterNeedDefinitionCatalog>();
            ICharacterRuntimeProfileFactory profiles = scope.Container
                .Resolve<ICharacterRuntimeProfileFactory>();
            IRandomStreamDiagnosticsQuery randomDiagnostics = scope.Container
                .Resolve<IRandomStreamDiagnosticsQuery>();
            IPreparedStartPartyCommitService commit = scope.Container
                .Resolve<IPreparedStartPartyCommitService>();
            ICharacterLifeQuery life = scope.Container.Resolve<ICharacterLifeQuery>();
            ICharacterConsumablesApplication consumables = scope.Container
                .Resolve<ICharacterConsumablesApplication>();
            ICharacterWorldQuery characterWorld = scope.Container
                .Resolve<ICharacterWorldQuery>();
            DungeonSceneRuntimeReferences sceneRuntimes = scope.Container
                .Resolve<DungeonSceneRuntimeReferences>();
            InvasionSceneRuntimeReferences invasionRuntimes = scope.Container
                .Resolve<InvasionSceneRuntimeReferences>();
            DungeonAutosaveService autosave = scope.Container
                .Resolve<IDungeonSaveCommandService>() as DungeonAutosaveService;
            runtimeReady = true;

            Check(scope != null && preparation != null && panel != null
                    && eventSystem != null && needs != null && profiles != null
                    && randomDiagnostics != null && commit != null && life != null
                    && consumables != null && characterWorld != null
                    && sceneRuntimes?.RunVariables != null && invasionRuntimes?.Threat != null
                    && autosave != null,
                "RUNTIME_DEPENDENCIES",
                $"scope={scope != null}; preparation={preparation != null}; panel={panel != null}; "
                + $"eventSystem={eventSystem != null}; needs={needs != null}; profiles={profiles != null}; "
                + $"random={randomDiagnostics != null}; commit={commit != null}; life={life != null}; "
                + $"consumables={consumables != null}; world={characterWorld != null}; "
                + $"autosave={autosave != null}");
            if (preparation == null || panel == null || eventSystem == null
                || needs == null || profiles == null || randomDiagnostics == null
                || commit == null || life == null || consumables == null || characterWorld == null
                || sceneRuntimes?.RunVariables == null || invasionRuntimes?.Threat == null
                || autosave == null)
            {
                yield break;
            }

            Button owner = FindButtonPrefix("OwnerOption_");
            Check(owner != null, "OWNER_BUTTON", "production owner option is visible");
            if (owner == null)
            {
                yield break;
            }

            PointerEventData pointer = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left
            };
            bool eventHandled = ExecuteEvents.Execute(
                owner.gameObject,
                pointer,
                ExecuteEvents.pointerClickHandler);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Check(eventHandled && preparation.IsPreparing,
                "EVENTSYSTEM_OWNER_TO_PREPARATION",
                $"handled={eventHandled}; preparing={preparation.IsPreparing}");
            if (!preparation.IsPreparing)
            {
                yield break;
            }

            VerifyAllPreparedSummaries(preparation, needs, profiles);

            yield return PrepareExistingFirstActives(preparation, 180f);
            bool ready = preparation.Members.All(member => member != null && member.IsReadyToStart);
            Check(ready, "PREPARED_PARTY_READY", "prepared candidates can produce the gameplay snapshot");
            if (!ready)
            {
                yield break;
            }

            VerifyReRenderDoesNotAdvancePreparationRandom(
                preparation,
                panel,
                randomDiagnostics);
            yield return VerifyVisibleScrollableCards(preparation, eventSystem);

            if (!TryCreateCommitEquivalentSnapshot(
                    preparation,
                    sceneRuntimes,
                    invasionRuntimes,
                    out PreparedStartPartySnapshot initialSnapshot,
                    out string initialSnapshotMessage))
            {
                Check(false, "PREPARED_SNAPSHOT", initialSnapshotMessage);
                yield break;
            }
            VerifySnapshotUsesPreparedHealth(preparation, initialSnapshot);
            Check(initialSnapshot.IsValid, "PREPARED_SNAPSHOT_VALID", initialSnapshotMessage);

            StartPartyMemberPreparation selected = preparation.Members
                .FirstOrDefault(member => member != null && !member.IsOwner);
            if (selected == null)
            {
                Check(false, "SELECTED_STAFF", "no selected staff candidate");
                yield break;
            }

            string skillBefore = Capture(preparation, selected.Index);
            bool skillRerolled = preparation.TryPartialReroll(
                selected.Index,
                StartPartyRerollGroup.Skill,
                out string skillMessage);
            yield return null;
            Check(skillRerolled && string.Equals(skillBefore, Capture(preparation, selected.Index),
                    StringComparison.Ordinal),
                "SKILL_REROLL_PRESERVES_LIVING_FACTS",
                skillMessage);

            bool fullyRerolled = preparation.TryFullReroll(selected.Index, out string fullMessage);
            yield return null;
            Check(fullyRerolled, "FULL_REROLL", fullMessage);
            VerifyPreparedSummary(preparation, selected, needs, profiles, "FULL_REROLL_LIVING_FACTS");

            StartPartyMemberPreparation reserve = preparation.Reserves
                .FirstOrDefault(member => member != null);
            if (reserve == null)
            {
                Check(false, "RESERVE_CANDIDATE", "no reserve candidate");
                yield break;
            }

            string selectedBeforeSwap = Capture(preparation, selected.Index);
            string reserveBeforeSwap = Capture(preparation, reserve.Index);
            bool swapped = preparation.TrySwapWithReserve(
                selected.Index,
                reserve.Index,
                out string swapMessage);
            yield return null;
            Check(swapped
                    && string.Equals(selectedBeforeSwap, Capture(preparation, selected.Index), StringComparison.Ordinal)
                    && string.Equals(reserveBeforeSwap, Capture(preparation, reserve.Index), StringComparison.Ordinal),
                "RESERVE_SWAP_PRESERVES_LIVING_FACTS",
                swapMessage);

            if (!TryCreateCommitEquivalentSnapshot(
                    preparation,
                    sceneRuntimes,
                    invasionRuntimes,
                    out PreparedStartPartySnapshot commitSnapshot,
                    out string commitSnapshotMessage))
            {
                Check(false, "COMMIT_SNAPSHOT", commitSnapshotMessage);
                yield break;
            }

            VerifySnapshotUsesPreparedHealth(preparation, commitSnapshot);
            Dictionary<int, StartPartyCandidateLivingSummary> committedLivingFacts =
                CaptureLivingSummariesByRoster(preparation);
            Time.timeScale = 0f;
            // This fixture ends the temporary committed run immediately. Reuse the
            // autosave service's lifecycle cleanup so Application.quitting cannot
            // persist the intentionally transient Tier-0 layout during teardown.
            autosave.Dispose();
            bool committed = commit.TryCommit(out string commitMessage);
            Check(committed, "PREPARED_START_COMMIT", commitMessage);
            if (!committed)
            {
                yield break;
            }

            yield return null;
            Canvas.ForceUpdateCanvases();
            VerifyCommittedLivingFacts(
                commitSnapshot,
                life,
                consumables,
                characterWorld,
                needs,
                committedLivingFacts);
            Check(!preparation.IsPreparing,
                "COMMIT_CANCELS_PREPARATION",
                "production commit released the temporary preparation state");
            results.Add("PASS SAFE_CLEANUP: no save API was called; fixture autosave was disposed before temporary commit and PlayMode exit discards fixture state.");
    }

    private IEnumerator PrepareExistingFirstActives(
        IStartPartyPreparationService preparation,
        float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            foreach (StartPartyMemberPreparation member in preparation.Members
                         .Where(member => member != null
                             && !member.IsOwner
                             && member.HasReadyFirstActive
                             && member.HasFirstPassive
                             && !member.HasSelectedFirstActive))
            {
                preparation.TryChooseFirstActive(member.Index, candidateIndex: 0, out _);
            }

            if (preparation.Members.All(member => member != null && member.IsReadyToStart))
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.25f);
        }
    }

    private void VerifyAllPreparedSummaries(
        IStartPartyPreparationService preparation,
        ICharacterNeedDefinitionCatalog needs,
        ICharacterRuntimeProfileFactory profiles)
    {
        foreach (StartPartyMemberPreparation member in preparation.Roster)
        {
            VerifyPreparedSummary(preparation, member, needs, profiles, "PREPARED_SUMMARY_" + member.Index);
        }
    }

    private void VerifyPreparedSummary(
        IStartPartyPreparationService preparation,
        StartPartyMemberPreparation member,
        ICharacterNeedDefinitionCatalog needs,
        ICharacterRuntimeProfileFactory profiles,
        string id)
    {
        bool read = preparation.TryGetCandidateLivingSummary(
            member.Index,
            out StartPartyCandidateLivingSummary summary,
            out string message);
        Check(read, id + "_READ", message);
        if (!read)
        {
            return;
        }

        string[] expectedConditionIds = NormalizedConditionIds(member.Progression?.GrowthState?.startingProfile);
        string[] actualConditionIds = summary.InitialHealthConditions
            .Select(condition => condition.ConditionId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        bool healthMatches = expectedConditionIds.SequenceEqual(actualConditionIds, StringComparer.Ordinal)
            && summary.InitialHealthConditions.All(condition => condition.Severity == AgeConditionSeverity.Mild);
        Check(healthMatches, id + "_HEALTH",
            $"expected={string.Join(",", expectedConditionIds)}; actual={string.Join(",", actualConditionIds)}");

        bool hasSleep = needs.TryGet(CharacterCondition.SLEEP, out CharacterNeedDefinition sleepDefinition);
        CharacterRuntimeProfile expectedProfile = profiles.Create(CharacterSpawnRequest.FromAuthoring(
            member.CharacterData,
            member.Progression.ResolveSelectedTraits()));
        SpeciesThermalProfile expectedThermal = expectedProfile.GetEnvironmentProfile().ToThermalProfile();
        bool livingMatches = summary.DietPolicy == CharacterConsumablesPolicyRules.DefaultDietPolicy
            && hasSleep
            && Mathf.Approximately(summary.InitialSleep, sleepDefinition.DefaultValue)
            && Mathf.Approximately(summary.SleepRateMultiplier, expectedProfile.GetNeedProfile().sleepRateMultiplier)
            && ThermalMatches(summary.ThermalProfile, expectedThermal);
        Check(livingMatches, id + "_DIET_SLEEP_CLIMATE",
            $"diet={summary.DietPolicy}; sleep={summary.InitialSleep:0.###}; "
            + $"sleepRate={summary.SleepRateMultiplier:0.###}; hasSleep={hasSleep}");
    }

    private void VerifyReRenderDoesNotAdvancePreparationRandom(
        IStartPartyPreparationService preparation,
        OwnerSelectionPanel panel,
        IRandomStreamDiagnosticsQuery randomDiagnostics)
    {
        bool foundBefore = TryCaptureRandomStream(
            randomDiagnostics,
            PreparationRandomStreamId,
            out RandomStreamDiagnosticSnapshot before);
        Dictionary<int, string> beforeSummaries = CaptureAll(preparation);
        panel.RefreshVisibility();
        Canvas.ForceUpdateCanvases();
        panel.RefreshVisibility();
        Canvas.ForceUpdateCanvases();
        bool foundAfter = TryCaptureRandomStream(
            randomDiagnostics,
            PreparationRandomStreamId,
            out RandomStreamDiagnosticSnapshot after);
        bool unchanged = foundBefore
            && foundAfter
            && before.State == after.State
            && before.DrawCount == after.DrawCount
            && FingerprintsMatch(beforeSummaries, CaptureAll(preparation));
        Check(unchanged,
            "RERENDER_DOES_NOT_REROLL",
            $"stream={PreparationRandomStreamId}; beforeState={before.State}; afterState={after.State}; "
            + $"beforeDraws={before.DrawCount}; afterDraws={after.DrawCount}; "
            + $"streamPresentBefore={foundBefore}; streamPresentAfter={foundAfter}");
    }

    private IEnumerator VerifyVisibleScrollableCards(
        IStartPartyPreparationService preparation,
        EventSystem eventSystem)
    {
        List<ScrollRect> cards = new List<ScrollRect>();
        foreach (StartPartyMemberPreparation member in preparation.Members)
        {
            ScrollRect scroll = FindMemberCard(member.Index);
            TMP_Text body = scroll?.content != null
                ? scroll.content.GetComponent<TMP_Text>()
                : null;
            bool complete = scroll != null
                && scroll.GetComponent<RectMask2D>() != null
                && scroll.viewport != null
                && scroll.content != null
                && scroll.vertical
                && !scroll.horizontal
                && body != null
                && body.overflowMode == TextOverflowModes.Overflow
                && body.text.Contains("생활·환경")
                && body.text.Contains("식단 정책")
                && body.text.Contains("시작 수면")
                && body.text.Contains("기후 온도 범위");
            Check(complete, "SCROLLABLE_LIVING_CARD_" + member.Index,
                scroll == null
                    ? "viewport missing"
                    : $"content={scroll.content != null}; viewport={scroll.viewport != null}; "
                        + $"contentHeight={scroll.content?.rect.height:0.#}; viewportHeight={scroll.viewport?.rect.height:0.#}");
            if (complete)
            {
                cards.Add(scroll);
            }
        }

        Canvas.ForceUpdateCanvases();
        yield return null;
        ScrollRect overflow = cards.FirstOrDefault(candidate => candidate.content != null
            && candidate.viewport != null
            && candidate.content.rect.height > candidate.viewport.rect.height + 1f);
        Check(overflow != null,
            "OVERFLOW_CARD",
            overflow == null
                ? "no visible prepared-member card overflowed; scroll interaction cannot be proven"
                : $"contentHeight={overflow.content.rect.height:0.#}; viewportHeight={overflow.viewport.rect.height:0.#}");
        if (overflow == null)
        {
            yield break;
        }

        overflow.verticalNormalizedPosition = 1f;
        Canvas.ForceUpdateCanvases();
        yield return null;
        float before = overflow.verticalNormalizedPosition;
        float overflowHeight = overflow.content.rect.height - overflow.viewport.rect.height;
        Vector2 localStart = overflow.viewport.rect.center;
        float localTravel = Mathf.Min(
            Mathf.Max(1f, overflow.viewport.rect.yMax - localStart.y - 1f),
            Mathf.Max(1f, overflowHeight + 1f));
        int gestureCount = Mathf.Max(
            1,
            Mathf.CeilToInt((overflowHeight + 1f) / localTravel));
        int executedGestures = 0;
        Canvas canvas = overflow.GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null
            && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        for (int gesture = 0;
             gesture < gestureCount
                && overflow.verticalNormalizedPosition > 0.01f;
             gesture++)
        {
            executedGestures++;
            Vector2 start = RectTransformUtility.WorldToScreenPoint(
                canvasCamera,
                overflow.viewport.TransformPoint(localStart));
            PointerEventData hitTest = new PointerEventData(eventSystem)
            {
                position = start
            };
            List<RaycastResult> hits = new List<RaycastResult>();
            eventSystem.RaycastAll(hitTest, hits);
            RaycastResult pressRaycast = hits.FirstOrDefault(hit => hit.gameObject != null
                && (hit.gameObject == overflow.gameObject
                    || hit.gameObject.transform.IsChildOf(overflow.transform)));
            Camera eventCamera = pressRaycast.module != null
                ? pressRaycast.module.eventCamera
                : canvasCamera;
            Vector2 end = RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                overflow.viewport.TransformPoint(localStart + Vector2.up * localTravel));
            PointerEventData drag = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = start,
                pressPosition = start,
                pointerDrag = overflow.gameObject,
                pointerPressRaycast = pressRaycast,
                pointerCurrentRaycast = pressRaycast
            };
            ExecuteEvents.Execute(overflow.gameObject, drag, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(overflow.gameObject, drag, ExecuteEvents.beginDragHandler);
            drag.delta = end - start;
            drag.position = end;
            ExecuteEvents.Execute(overflow.gameObject, drag, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(overflow.gameObject, drag, ExecuteEvents.endDragHandler);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }
        float afterDrag = overflow.verticalNormalizedPosition;
        bool moved = afterDrag < before - 0.01f;

        TMP_Text finalBody = overflow.content.GetComponent<TMP_Text>();
        bool finalSectionReachedByDrag = afterDrag <= 0.01f
            && finalBody != null
            && finalBody.text.Contains("기후 온도 범위")
            && finalBody.text.Contains("치명");
        Check(moved && finalSectionReachedByDrag,
            "EVENTSYSTEM_SCROLL_REACHES_FINAL_CLIMATE",
            $"before={before:0.###}; afterDrag={afterDrag:0.###}; "
            + $"reachedBottom={finalSectionReachedByDrag}; "
            + $"gestures={executedGestures}/{gestureCount}; localTravel={localTravel:0.###}");
    }

    private static ScrollRect FindMemberCard(int memberIndex) =>
        Resources.FindObjectsOfTypeAll<ScrollRect>()
            .FirstOrDefault(candidate => candidate != null
                && candidate.gameObject.scene.IsValid()
                && candidate.gameObject.activeInHierarchy
                && string.Equals(candidate.name,
                    "StartPartyMemberBodyViewport_" + memberIndex,
                    StringComparison.Ordinal));

    private static bool TryCaptureRandomStream(
        IRandomStreamDiagnosticsQuery diagnostics,
        string streamId,
        out RandomStreamDiagnosticSnapshot snapshot)
    {
        snapshot = default;
        if (diagnostics == null)
        {
            return false;
        }

        foreach (RandomStreamDiagnosticSnapshot candidate in diagnostics.Capture())
        {
            if (string.Equals(candidate.StreamId, streamId, StringComparison.Ordinal))
            {
                snapshot = candidate;
                return true;
            }
        }

        return false;
    }

    private void VerifySnapshotUsesPreparedHealth(
        IStartPartyPreparationService preparation,
        PreparedStartPartySnapshot snapshot)
    {
        foreach (PreparedStartPartyMemberSnapshot persisted in snapshot.OrderedMembers)
        {
            StartPartyMemberPreparation prepared = preparation.Members.FirstOrDefault(member => member != null
                && member.RosterId == persisted.rosterId);
            string[] preparedIds = NormalizedConditionIds(prepared?.Progression?.GrowthState?.startingProfile);
            string[] snapshotIds = NormalizedConditionIds(persisted?.growth?.startingProfile);
            Check(prepared != null && preparedIds.SequenceEqual(snapshotIds, StringComparer.Ordinal),
                "SNAPSHOT_HEALTH_AUTHORITY_" + persisted.rosterId,
                $"prepared={string.Join(",", preparedIds)}; snapshot={string.Join(",", snapshotIds)}");

            int[] preparedTraitIds = (prepared?.Progression?.GrowthState?.traitIds
                ?? new List<int>())
                .OrderBy(value => value)
                .ToArray();
            int[] snapshotTraitIds = (persisted?.growth?.traitIds
                ?? new List<int>())
                .OrderBy(value => value)
                .ToArray();
            Check(prepared != null && preparedTraitIds.SequenceEqual(snapshotTraitIds),
                "SNAPSHOT_THERMAL_TRAIT_AUTHORITY_" + persisted.rosterId,
                $"prepared={string.Join(",", preparedTraitIds)}; snapshot={string.Join(",", snapshotTraitIds)}");
        }
    }

    private static bool TryCreateCommitEquivalentSnapshot(
        IStartPartyPreparationService preparation,
        DungeonSceneRuntimeReferences sceneRuntimes,
        InvasionSceneRuntimeReferences invasionRuntimes,
        out PreparedStartPartySnapshot snapshot,
        out string message)
    {
        DungeonDifficulty difficulty = DungeonDifficulty.Normal;
        if (invasionRuntimes?.Threat?.Settings != null)
        {
            difficulty = DungeonDifficultyRules.FromLegacy(
                invasionRuntimes.Threat.Settings.difficulty);
        }

        int runSeed = sceneRuntimes?.RunVariables?.RunSeed ?? 0;
        if (runSeed == 0)
        {
            snapshot = null;
            message = "commit-equivalent runtime run seed is unavailable";
            return false;
        }

        return preparation.TryCreatePreparedSnapshot(
            difficulty,
            runSeed,
            out snapshot,
            out message);
    }

    private Dictionary<int, StartPartyCandidateLivingSummary> CaptureLivingSummariesByRoster(
        IStartPartyPreparationService preparation)
    {
        Dictionary<int, StartPartyCandidateLivingSummary> summaries = new();
        foreach (StartPartyMemberPreparation member in preparation.Members)
        {
            if (member == null)
            {
                continue;
            }

            bool read = preparation.TryGetCandidateLivingSummary(
                member.Index,
                out StartPartyCandidateLivingSummary summary,
                out string message);
            Check(read, "COMMIT_EXPECTED_LIVING_" + member.RosterId, message);
            if (read)
            {
                summaries[member.RosterId] = summary;
            }
        }

        return summaries;
    }

    private void VerifyCommittedLivingFacts(
        PreparedStartPartySnapshot snapshot,
        ICharacterLifeQuery life,
        ICharacterConsumablesApplication consumables,
        ICharacterWorldQuery characterWorld,
        ICharacterNeedDefinitionCatalog needs,
        IReadOnlyDictionary<int, StartPartyCandidateLivingSummary> expectedLivingFacts)
    {
        bool hasSleep = needs.TryGet(CharacterCondition.SLEEP, out CharacterNeedDefinition sleep);
        foreach (PreparedStartPartyMemberSnapshot member in snapshot.OrderedMembers)
        {
            bool hasExpected = expectedLivingFacts.TryGetValue(
                member.rosterId,
                out StartPartyCandidateLivingSummary expected);
            CharacterId expectedId = (CharacterId)(member.persistentId ?? string.Empty);
            CharacterActor actor = expectedId.IsValid
                ? characterWorld.Characters.FirstOrDefault(candidate => candidate != null
                    && CharacterPersistentIdentity.TryGet(candidate, out CharacterId candidateId)
                    && candidateId.Equals(expectedId))
                : null;
            CharacterLifeRecord record = null;
            bool hasLife = actor != null && life.TryGet(expectedId, out record);
            Check(hasExpected && actor != null && hasLife,
                "LIVE_MEMBER_RESOLUTION_" + member.rosterId,
                $"expectedId={expectedId.Value}; summary={hasExpected}; actor={actor != null}; life={hasLife}");
            if (!hasExpected || actor == null || !hasLife)
            {
                continue;
            }

            string[] expectedConditions = expected.InitialHealthConditions
                .Select(condition => condition.ConditionId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] liveConditions = record.AgeConditions
                .Select(condition => condition.ConditionId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            bool healthMatches = expectedConditions.SequenceEqual(
                    liveConditions,
                    StringComparer.Ordinal)
                && record.AgeConditions.All(condition => condition.Severity == AgeConditionSeverity.Mild);
            Check(healthMatches,
                "LIVE_INITIAL_HEALTH_" + member.rosterId,
                $"expected={string.Join(",", expectedConditions)}; live={string.Join(",", liveConditions)}");

            bool dietMatches = consumables.GetDietPolicy(expectedId)
                == CharacterConsumablesPolicyRules.DefaultDietPolicy;
            bool sleepMatches = hasSleep
                && actor.Stats != null
                && actor.Stats.TryGetConditionValue(CharacterCondition.SLEEP, out float liveSleep)
                && Mathf.Approximately(liveSleep, expected.InitialSleep)
                && Mathf.Approximately(liveSleep, sleep.DefaultValue);
            CharacterRuntimeProfile liveProfile = actor.profile;
            SpeciesThermalProfile liveThermal = liveProfile != null
                ? liveProfile.GetEnvironmentProfile().ToThermalProfile()
                : default;
            bool climateMatches = liveProfile != null
                && ThermalMatches(expected.ThermalProfile, liveThermal);
            Check(dietMatches && sleepMatches && climateMatches,
                "LIVE_DIET_SLEEP_CLIMATE_" + member.rosterId,
                $"diet={consumables.GetDietPolicy(expectedId)}; expectedSleep={expected.InitialSleep:0.###}; "
                + $"hasSleep={hasSleep}; profile={liveProfile != null}");
        }
    }

    private static string[] NormalizedConditionIds(CharacterStartingProfileState profile) =>
        (profile?.initialAgeConditionIds ?? new List<string>())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.Ordinal)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static bool ThermalMatches(SpeciesThermalProfile left, SpeciesThermalProfile right) =>
        Mathf.Approximately(left.ComfortMinimum, right.ComfortMinimum)
        && Mathf.Approximately(left.ComfortMaximum, right.ComfortMaximum)
        && Mathf.Approximately(left.SafeMinimum, right.SafeMinimum)
        && Mathf.Approximately(left.SafeMaximum, right.SafeMaximum)
        && Mathf.Approximately(left.LethalMinimum, right.LethalMinimum)
        && Mathf.Approximately(left.LethalMaximum, right.LethalMaximum);

    private static Dictionary<int, string> CaptureAll(IStartPartyPreparationService preparation) =>
        preparation.Roster.ToDictionary(member => member.Index, member => Capture(preparation, member.Index));

    private static string Capture(IStartPartyPreparationService preparation, int memberIndex)
    {
        if (!preparation.TryGetCandidateLivingSummary(memberIndex, out StartPartyCandidateLivingSummary summary,
                out string message))
        {
            return "ERROR:" + message;
        }

        return string.Join(",", summary.InitialHealthConditions.Select(condition =>
                condition.ConditionId + ":" + condition.Severity)
                .OrderBy(value => value, StringComparer.Ordinal))
            + $"|{summary.DietPolicy}|{summary.InitialSleep:0.######}|{summary.SleepRateMultiplier:0.######}"
            + $"|{summary.ThermalProfile.ComfortMinimum:0.######}:{summary.ThermalProfile.ComfortMaximum:0.######}"
            + $"|{summary.ThermalProfile.SafeMinimum:0.######}:{summary.ThermalProfile.SafeMaximum:0.######}"
            + $"|{summary.ThermalProfile.LethalMinimum:0.######}:{summary.ThermalProfile.LethalMaximum:0.######}";
    }

    private static bool FingerprintsMatch(
        IReadOnlyDictionary<int, string> before,
        IReadOnlyDictionary<int, string> after) =>
        before.Count == after.Count
        && before.All(pair => after.TryGetValue(pair.Key, out string value)
            && string.Equals(pair.Value, value, StringComparison.Ordinal));

    private static Button FindButtonPrefix(string prefix) =>
        Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.interactable
                && button.name.StartsWith(prefix, StringComparison.Ordinal));

    private void Check(bool condition, string id, string detail)
    {
        string line = $"{(condition ? "PASS" : "FAIL")} {id}: {detail}";
        results.Add(line);
        if (!condition)
        {
            failures.Add(line);
        }
    }

    private void Finish()
    {
        results.Insert(0, "WIM042 prepared candidate living information verification");
        results.Add($"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}; failures={failures.Count}");
        results.AddRange(failures.Select(failure => "FAILURE_DETAIL " + failure));
        File.WriteAllLines(Wim042PreparedCandidateLivingInfoPlayModeVerifier.ReportPath, results);
        if (failures.Count == 0)
        {
            Debug.Log("WIM042 prepared candidate living information verification passed.");
        }
        else
        {
            Debug.LogError("WIM042 prepared candidate living information verification failed: "
                + string.Join(" | ", failures));
        }
    }
}
#endif
