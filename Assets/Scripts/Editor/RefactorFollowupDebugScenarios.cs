using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class RefactorFollowupDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Refactor Followup/Run PlayMode Scene Smoke")]
    public static void RunPlayModeSceneSmokeFromMenu()
    {
        RunPlayModeSceneSmoke(log: true);
    }

    public static bool RunPlayModeSceneSmoke(bool log = false)
    {
        List<string> errors = new List<string>();

        if (!Application.isPlaying)
        {
            errors.Add("- Refactor follow-up smoke requires PlayMode.");
            Report(errors, log);
            return false;
        }

        RunScenario("Building info inactive panel open/close", VerifyBuildingInfoOpenClose, errors);
        RunScenario("Summary tabs render non-empty text", VerifySummaryTabsRender, errors);
        RunScenario("Character memory and feedback bubble runtime APIs", VerifyCharacterMemoryAndBubble, errors);
        RunScenario("Cached scene runtime providers resolve/fail clearly", VerifyCachedRuntimeProviders, errors);
        RunScenario("Facility evolution debug scenarios", () => FacilityEvolutionDebugScenarios.RunAll(log: false), errors);

        return Report(errors, log);
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (!scenario())
            {
                errors.Add($"- {name}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"- {name}: {ex.GetType().Name} {ex.Message}");
        }
    }

    private static bool Report(List<string> errors, bool log)
    {
        if (errors.Count > 0)
        {
            Debug.LogError($"RefactorFollowupDebugScenarios failed:\n{string.Join("\n", errors)}");
            return false;
        }

        if (log)
        {
            Debug.Log("RefactorFollowupDebugScenarios passed.");
        }

        return true;
    }

    private static bool VerifyBuildingInfoOpenClose()
    {
        UIBuildingInfo info = UnityEngine.Object.FindFirstObjectByType<UIBuildingInfo>(FindObjectsInactive.Include);
        BuildableObject building = UnityEngine.Object
            .FindObjectsByType<BuildableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault((candidate) => candidate != null
                && !candidate.isDestroy
                && candidate.BuildingData != null);

        if (info == null || building == null)
        {
            return false;
        }

        info.CloseDispaly();
        info.DisplayBuildingInfo(building);
        bool opened = info.gameObject.activeInHierarchy;
        info.CloseDispaly();
        return opened;
    }

    private static bool VerifySummaryTabsRender()
    {
        UITabManager manager = UnityEngine.Object.FindFirstObjectByType<UITabManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            return false;
        }

        foreach (int id in new[] { 5, 6, 7, 8, 9 })
        {
            manager.ToggleSelectButton(id);
            UITab[] activeTabs = UnityEngine.Object
                .FindObjectsByType<UITab>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where((tab) => tab != null && tab.gameObject.activeInHierarchy)
                .ToArray();

            int textLength = activeTabs
                .SelectMany((tab) => tab.GetComponentsInChildren<TMP_Text>(includeInactive: true))
                .Where((text) => text != null && !string.IsNullOrWhiteSpace(text.text))
                .Sum((text) => text.text.Length);

            manager.ToggleSelectButton(id);
            if (activeTabs.Length != 1 || textLength == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool VerifyCharacterMemoryAndBubble()
    {
        CharacterActor actor = UnityEngine.Object.FindFirstObjectByType<CharacterActor>(FindObjectsInactive.Include);
        if (actor == null)
        {
            return false;
        }

        actor.EnsureRuntimeState();
        CharacterSocialMemory memory = actor.GetComponent<CharacterSocialMemory>();
        CharacterFeedbackBubble bubble = actor.GetComponent<CharacterFeedbackBubble>();
        if (memory == null || bubble == null)
        {
            return false;
        }

        memory.Bind(actor);
        float trust = memory.GetSourceTrust(actor);
        bubble.Show(CharacterFeedbackState.Joy);
        return Mathf.Approximately(trust, 1f)
            && bubble.CurrentState == CharacterFeedbackState.Joy;
    }

    private static bool VerifyCachedRuntimeProviders()
    {
        DungeonSceneComponentQuery query = new DungeonSceneComponentQuery();
        CharacterSceneRuntimeReferences characterReferences =
            new CharacterSceneRuntimeReferences(
                query.First<LocalLlmRequestQueue>(includeInactive: true),
                query.First<SocialReputationRuntime>(includeInactive: true),
                query.First<StaffDiscontentRuntime>(includeInactive: true),
                query.First<RegularCustomerRuntime>(includeInactive: true),
                query.First<CharacterSpawner>(includeInactive: true),
                query.First<CharacterAiScheduler>(includeInactive: true),
                query.First<OwnerRunManager>(includeInactive: true),
                query.First<AiDirectorRuntime>(includeInactive: true));
        ProgressionSceneRuntimeReferences progressionReferences =
            new ProgressionSceneRuntimeReferences(
                query.First<DailyFacilityShopRuntime>(includeInactive: true),
                query.First<BlueprintResearchRuntime>(includeInactive: true),
                query.First<MetaProgressionRuntime>(includeInactive: true));

        bool localLlmFound = new LocalLlmRuntimeProvider(characterReferences)
            .TryGetRuntime(out ILocalLlmRuntime localLlmRuntime);
        bool socialFound = new SocialReputationRuntimeProvider(characterReferences)
            .TryGetRuntime(out SocialReputationRuntime socialRuntime);
        bool optionalMissingIsFalse = !new BlueprintResearchRuntimeProvider(
                progressionReferences)
            .TryGetRuntime(out BlueprintResearchRuntime _);
        FacilityFeatureSceneRuntimeReferences facilityReferences =
            new FacilityFeatureSceneRuntimeReferences(null, null, null);
        bool requiredMissingIsClear = ThrowsInvalidOperation(
            () => _ = new FacilityEvolutionRuntimeProvider(facilityReferences).Runtime);

        return localLlmFound
            && localLlmRuntime != null
            && socialFound
            && socialRuntime != null
            && optionalMissingIsFalse
            && requiredMissingIsClear;
    }

    private static bool ThrowsInvalidOperation(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }
}
