using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class OwnerDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Character/Run P1 Owner Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 owner scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("사장 후보 에셋", VerifyOwnerCandidateAssets, errors);
        RunScenario("사장 역할 런타임 연결", VerifyOwnerRuntimeRole, errors);
        RunScenario("사장 자동 작업 액션", VerifyOwnerAiActions, errors);
        RunScenario("사장 사망 런 종료", VerifyOwnerDeathEndsRun, errors);
        RunScenario("사장 우선 작업 지정", VerifyOwnerPriorityWork, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("P1 owner scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifyOwnerCandidateAssets()
    {
        CharacterSO[] owners = LoadOwners();
        return owners.Length == 3
            && owners.All((owner) => owner != null
                && owner.IsOwnerCandidate
                && owner.characterType == CharacterType.NPC
                && owner.species != null
                && owner.traits != null
                && owner.traits.Length > 0
                && owner.characterSprite != null
                && !string.IsNullOrWhiteSpace(owner.ownerSummary)
                && owner.HasOwnerPreferredWorkTypes)
            && owners.Any((owner) => owner.SpeciesTag == "Slime")
            && owners.Any((owner) => owner.SpeciesTag == "Orc")
            && owners.Any((owner) => owner.SpeciesTag == "Vampire");
    }

    private static bool VerifyOwnerRuntimeRole()
    {
        CharacterSO ownerData = LoadOwner("Owner_Orc");
        GameObject obj = CreateCharacterObject("Owner Runtime Scenario");
        CharacterActor character = obj.GetComponent<CharacterActor>();

        InitializeCharacter(character, ownerData);

        bool valid = character.IsOwner
            && !character.CanLeaveByDissatisfaction
            && !character.CanRebel
            && character.MaxHealth > 100f
            && Mathf.Approximately(character.CurrentHealth, character.MaxHealth)
            && character.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Guard) > 1f;

        Object.DestroyImmediate(obj);
        return valid;
    }

    private static bool VerifyOwnerAiActions()
    {
        CharacterSO ownerData = LoadOwner("Owner_Slime");
        GameObject obj = CreateCharacterObject("Owner AI Scenario");
        CharacterActor character = obj.GetComponent<CharacterActor>();

        InitializeCharacter(character, ownerData);
        AIAction[] actions = character.ai.availableActions;
        bool valid = actions.Any((action) => action.actionset is AIWork)
            && actions.Any((action) => action.actionset is AIWait)
            && !actions.Any((action) => action.actionset is AIExitDungeon);

        Object.DestroyImmediate(obj);
        return valid;
    }

    private static bool VerifyOwnerDeathEndsRun()
    {
        CharacterSO ownerData = LoadOwner("Owner_Vampire");
        GameObject managerObject = new GameObject("Owner Death Scenario Manager");
        OwnerRunManager manager = managerObject.AddComponent<OwnerRunManager>();
        CharacterAiEditorTestDependencies.Inject(manager);
        manager.SelectOwner(ownerData);

        CharacterActor owner = manager.CurrentOwnerActor;
        if (owner == null)
        {
            Object.DestroyImmediate(managerObject);
            return false;
        }

        owner.ApplyDamage(owner.MaxHealth + 1f, "테스트 피해");
        bool valid = manager.IsRunEnded;

        if (owner != null)
        {
            Object.DestroyImmediate(owner.gameObject);
        }
        Object.DestroyImmediate(managerObject);
        return valid;
    }

    private static bool VerifyOwnerPriorityWork()
    {
        CharacterSO ownerData = LoadOwner("Owner_Vampire");
        GameObject characterObject = CreateCharacterObject("Owner Priority Scenario");
        CharacterActor character = characterObject.GetComponent<CharacterActor>();
        InitializeCharacter(character, ownerData);
        GameObject runtimeObject = new GameObject("Owner Priority Research Runtime");
        BlueprintResearchRuntime researchRuntime = runtimeObject.AddComponent<BlueprintResearchRuntime>();
        CharacterAiEditorTestDependencies.Inject(researchRuntime);
        researchRuntime.EnqueueBlueprint(AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>(
            "Assets/Resources/SO/Blueprint/P1/BP_SupportBasics.asset"));

        BuildingSO labData = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/P1_ResearchLab.asset");
        GameObject labObject = new GameObject("Research Lab Priority Target");
        Facility lab = labObject.AddComponent<Facility>();
        CharacterAiEditorTestDependencies.Inject(lab);
        lab.Initialization(labData, Vector2Int.zero);

        bool valid = character.TryGetAbility(out AbilityWork work)
            && work.TrySetPriorityWorkTarget(lab, out _)
            && work.PriorityWorkTarget == lab
            && work.TryAssignShop()
            && work.assignedShop == lab;

        Object.DestroyImmediate(labObject);
        Object.DestroyImmediate(runtimeObject);
        Object.DestroyImmediate(characterObject);
        return valid;
    }

    private static GameObject CreateCharacterObject(string name)
    {
        GameObject obj = new GameObject(name);
        obj.AddComponent<SpriteRenderer>();
        obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        obj.AddComponent<AbilityWork>();
        obj.AddComponent<AIBrain>();
        CharacterAiEditorTestDependencies.Inject(obj);
        return obj;
    }

    private static void InitializeCharacter(CharacterActor character, CharacterSO data)
    {
        typeof(CharacterActor)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(character, null);

        character.RefreshAbilityCache();
        character.Initialization(data);
        character.SetLifecycleState(CharacterLifecycleState.Active);
    }

    private static CharacterSO[] LoadOwners()
    {
        return AssetDatabase.FindAssets("t:CharacterSO", new[] { "Assets/Resources/SO/Character/Owners" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CharacterSO>)
            .Where((owner) => owner != null)
            .OrderBy((owner) => owner.id)
            .ToArray();
    }

    private static CharacterSO LoadOwner(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<CharacterSO>(
            $"Assets/Resources/SO/Character/Owners/{assetName}.asset");
    }
}
