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
        RunScenario("Owner restore publication rollback/finalize", VerifyReversibleOwnerPublication, errors);
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

        try
        {
            InitializeCharacter(character, ownerData);

            return character.IsOwner
                && !character.CanLeaveByDissatisfaction
                && !character.CanRebel
                && character.MaxHealth > 100f
                && Mathf.Approximately(character.CurrentHealth, character.MaxHealth)
                && character.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Guard) > 1f;
        }
        finally
        {
            Object.DestroyImmediate(obj);
        }
    }

    private static bool VerifyOwnerAiActions()
    {
        CharacterSO ownerData = LoadOwner("Owner_Slime");
        GameObject obj = CreateCharacterObject("Owner AI Scenario");
        CharacterActor character = obj.GetComponent<CharacterActor>();

        try
        {
            InitializeCharacter(character, ownerData);
            AIAction[] actions = character.ai.availableActions;
            return actions.Any((action) => action.actionset is AIWork)
                && actions.Any((action) => action.actionset is AIWait)
                && !actions.Any((action) => action.actionset is AIExitDungeon);
        }
        finally
        {
            Object.DestroyImmediate(obj);
        }
    }

    private static bool VerifyOwnerDeathEndsRun()
    {
        CharacterSO ownerData = LoadOwner("Owner_Vampire");
        GameObject managerObject = new GameObject("Owner Death Scenario Manager");
        OwnerRunManager manager = managerObject.AddComponent<OwnerRunManager>();
        CharacterActor owner = null;

        try
        {
            CharacterAiEditorTestDependencies.Inject(manager);
            manager.SelectOwner(ownerData);
            owner = manager.CurrentOwnerActor;
            if (owner == null)
            {
                return false;
            }

            owner.ApplyDamage(owner.MaxHealth + 1f, "테스트 피해");
            return manager.IsRunEnded;
        }
        finally
        {
            if (owner != null)
            {
                Object.DestroyImmediate(owner.gameObject);
            }
            Object.DestroyImmediate(managerObject);
        }
    }

    private static bool VerifyReversibleOwnerPublication()
    {
        CharacterSO previousOwnerData = LoadOwner("Owner_Orc");
        CharacterSO restoredOwnerData = LoadOwner("Owner_Vampire");
        GameObject managerObject = new GameObject("Owner Publication Scenario Manager");
        OwnerRunManager manager = managerObject.AddComponent<OwnerRunManager>();
        CharacterActor rollbackCandidate = null;
        CharacterActor completedCandidate = null;
        CharacterActor previousOwner = null;

        try
        {
            CharacterAiEditorTestDependencies.Inject(manager);
            manager.SelectOwner(previousOwnerData);
            previousOwner = manager.CurrentOwnerActor;
            Data<CharacterSO> previousSelection = manager.selectedOwnerData;
            int selectionEvents = 0;
            manager.OnOwnerSelected += _ => selectionEvents++;

            rollbackCandidate = CreateDetachedOwnerCandidate(
                "Owner Rollback Candidate",
                restoredOwnerData);
            Transform rollbackCandidateParent = rollbackCandidate.transform.parent;
            OwnerRestorePublication rollbackPublication =
                manager.BeginRestoreCandidatePublication(
                    restoredOwnerData,
                    rollbackCandidate);

            bool pendingStateIsReversible =
                manager.HasPendingRestorePublication
                && manager.CurrentOwnerActor == rollbackCandidate
                && !rollbackCandidate.gameObject.activeSelf
                && previousOwner.gameObject.activeSelf
                && manager.selectedOwnerData.Value == restoredOwnerData
                && selectionEvents == 0
                && !manager.CompleteRun(DungeonRunOutcome.Defeat, "pending restore");

            manager.RollbackRestoreCandidatePublication(rollbackPublication);
            bool rollbackRestoredExactState =
                pendingStateIsReversible
                && !manager.HasPendingRestorePublication
                && rollbackPublication.IsRolledBack
                && manager.CurrentOwnerActor == previousOwner
                && previousOwner.gameObject.activeSelf
                && ReferenceEquals(manager.selectedOwnerData, previousSelection)
                && manager.selectedOwnerData.Value == previousOwnerData
                && rollbackCandidate != null
                && !rollbackCandidate.gameObject.activeSelf
                && rollbackCandidate.IsDetachedRestoreCandidate
                && rollbackCandidate.transform.parent == rollbackCandidateParent
                && selectionEvents == 0;

            completedCandidate = CreateDetachedOwnerCandidate(
                "Owner Completion Candidate",
                restoredOwnerData);
            OwnerRestorePublication completionPublication =
                manager.BeginRestoreCandidatePublication(
                    restoredOwnerData,
                    completedCandidate);
            manager.CompleteRestoreCandidatePublication(completionPublication);

            return rollbackRestoredExactState
                && !manager.HasPendingRestorePublication
                && completionPublication.IsCompleted
                && manager.CurrentOwnerActor == completedCandidate
                && completedCandidate.gameObject.activeSelf
                && previousOwner == null
                && manager.selectedOwnerData.Value == restoredOwnerData
                && selectionEvents == 1;
        }
        finally
        {
            if (rollbackCandidate != null)
            {
                Object.DestroyImmediate(rollbackCandidate.gameObject);
            }
            if (completedCandidate != null)
            {
                Object.DestroyImmediate(completedCandidate.gameObject);
            }
            if (previousOwner != null)
            {
                Object.DestroyImmediate(previousOwner.gameObject);
            }
            Object.DestroyImmediate(managerObject);
        }
    }

    private static bool VerifyOwnerPriorityWork()
    {
        CharacterSO ownerData = LoadOwner("Owner_Vampire");
        GameObject runtimeObject = new GameObject("Owner Priority Research Runtime");
        GameObject characterObject = null;
        GameObject labObject = null;

        try
        {
            BlueprintResearchRuntime researchRuntime =
                runtimeObject.AddComponent<BlueprintResearchRuntime>();
            CharacterAiEditorTestDependencies.Inject(researchRuntime);
            researchRuntime.EnqueueBlueprint(AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>(
                "Assets/Resources/SO/Blueprint/P1/BP_SupportBasics.asset"));

            characterObject = CreateCharacterObject(
                "Owner Priority Scenario",
                researchRuntime);
            CharacterActor character = characterObject.GetComponent<CharacterActor>();
            InitializeCharacter(character, ownerData);

            BuildingSO labData = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/P1/P1_ResearchLab.asset");
            labObject = new GameObject("Research Lab Priority Target");
            Facility lab = labObject.AddComponent<Facility>();
            CharacterAiEditorTestDependencies.Inject(lab, researchRuntime);
            lab.Initialization(labData, Vector2Int.zero);

            return character.TryGetAbility(out AbilityWork work)
                && work.TrySetPriorityWorkTarget(lab, out _)
                && work.PriorityWorkTarget == lab
                && work.TryAssignShop()
                && work.assignedShop == lab;
        }
        finally
        {
            if (labObject != null)
            {
                Object.DestroyImmediate(labObject);
            }
            if (characterObject != null)
            {
                Object.DestroyImmediate(characterObject);
            }
            Object.DestroyImmediate(runtimeObject);
        }
    }

    private static GameObject CreateCharacterObject(
        string name,
        BlueprintResearchRuntime researchRuntime = null)
    {
        GameObject obj = new GameObject(name);
        obj.AddComponent<SpriteRenderer>();
        obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        obj.AddComponent<AbilityWork>();
        obj.AddComponent<AIBrain>();
        if (researchRuntime != null)
        {
            CharacterAiEditorTestDependencies.Inject(obj, researchRuntime);
        }
        else
        {
            CharacterAiEditorTestDependencies.Inject(obj);
        }
        return obj;
    }

    private static CharacterActor CreateDetachedOwnerCandidate(
        string name,
        CharacterSO data)
    {
        GameObject obj = new GameObject(name);
        obj.SetActive(false);
        obj.AddComponent<SpriteRenderer>();
        CharacterActor actor = obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        obj.AddComponent<AbilityWork>();
        obj.AddComponent<AIBrain>();

        actor.PrepareForDetachedRestore();
        CharacterAiEditorTestDependencies.Inject(obj);
        actor.EnsureRuntimeState();
        actor.RefreshAbilityCache();
        actor.Initialization(data);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.Brain?.UseOwnerWorkActions();
        return actor;
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
