using System.Collections.Generic;
using BehaviorDesigner.Editor;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using BehaviorDesigner.Runtime.Tasks.DungeonStory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Task = BehaviorDesigner.Runtime.Tasks.Task;

public static class CharacterAiBehaviorDesignerGraphBuilder
{
    private const string ExternalBehaviorFolder = "Assets/Behavior Designer/External Behaviors";
    private const string CharacterAiExternalBehaviorPath =
        ExternalBehaviorFolder + "/CharacterAIExternalBehavior.asset";

    [MenuItem("DungeonStory/Debug/Character/Build Visual Character AI Behavior Trees")]
    public static void BuildVisualCharacterAiBehaviorTrees()
    {
        AssetDatabase.Refresh();
        ExternalBehaviorTree externalBehavior = EnsureCharacterAiExternalBehavior();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            int runtimeChanges = BuildRuntimeGraphs(externalBehavior);
            AssetDatabase.SaveAssets();
            RefreshOpenBehaviorDesignerWindow();
            Debug.Log(
                "Rebuilt visual character AI External Behavior asset. "
                + $"Updated runtime objects: {runtimeChanges}. Skipped prefab and scene writes during Play Mode.");
            return;
        }

        int prefabChanges = BuildPrefabGraphs(externalBehavior);
        int sceneChanges = BuildOpenSceneGraphs(externalBehavior);
        int prefabStageChanges = BuildCurrentPrefabStageGraph(externalBehavior);
        AssetDatabase.SaveAssets();
        RefreshOpenBehaviorDesignerWindow();
        Debug.Log(
            "Built visual character AI Behavior Designer graphs. "
            + $"Prefabs: {prefabChanges}, scene objects: {sceneChanges}, prefab stage actors: {prefabStageChanges}");
    }

    public static bool EnsureVisualGraph(BehaviorTree tree)
    {
        ExternalBehaviorTree externalBehavior = EnsureCharacterAiExternalBehavior();
        return EnsureBehaviorTreeUsesExternalGraph(tree, externalBehavior);
    }

    public static ExternalBehaviorTree EnsureCharacterAiExternalBehavior()
    {
        EnsureExternalBehaviorFolder();
        ExternalBehaviorTree externalBehavior =
            AssetDatabase.LoadAssetAtPath<ExternalBehaviorTree>(CharacterAiExternalBehaviorPath);
        if (externalBehavior == null)
        {
            externalBehavior = ScriptableObject.CreateInstance<ExternalBehaviorTree>();
            externalBehavior.name = "CharacterAIExternalBehavior";
            AssetDatabase.CreateAsset(externalBehavior, CharacterAiExternalBehaviorPath);
        }

        EnsureVisualGraph(externalBehavior);
        return externalBehavior;
    }

    public static bool EnsureVisualGraph(ExternalBehaviorTree externalBehavior)
    {
        if (externalBehavior == null)
        {
            return false;
        }

        BehaviorSource source = new BehaviorSource(externalBehavior)
        {
            behaviorName = "Character AI",
            behaviorDescription = "Shared visual Behavior Designer tree for DungeonStory character AI."
        };

        int id = 1;
        EntryTask entry = CreateTask<EntryTask>(id++, "Entry", string.Empty, new Vector2(0f, -260f));
        Selector root = CreateTask<Selector>(id++, "Character AI Root", string.Empty, new Vector2(0f, -140f));

        Sequence critical = CreateTask<Sequence>(id++, "Critical", string.Empty, new Vector2(-900f, 20f));
        HasCriticalState hasCritical = CreateTask<HasCriticalState>(id++, "Has Critical?", string.Empty, new Vector2(-1000f, 120f));
        RunCriticalState runCritical = CreateTask<RunCriticalState>(id++, "Run Critical", string.Empty, new Vector2(-800f, 120f));
        AddChildren(critical, hasCritical, runCritical);

        Sequence deprivationBreakdown = CreateTask<Sequence>(
            id++,
            "Deprivation Breakdown",
            string.Empty,
            new Vector2(-730f, 20f));
        AddChildren(
            deprivationBreakdown,
            CreateTask<HasDeprivationBreakdown>(
                id++,
                "Has Deprivation Breakdown?",
                string.Empty,
                new Vector2(-830f, 180f)),
            CreateTask<RunDeprivationBreakdown>(
                id++,
                "Run Deprivation Breakdown",
                string.Empty,
                new Vector2(-630f, 180f)));

        Sequence lockedAction = CreateLockedActionBranch(ref id, new Vector2(-560f, 20f));
        Sequence interruptCheck = CreateInterruptCheckBranch(ref id, new Vector2(-220f, 20f));

        Selector macro = CreateTask<Selector>(id++, "Macro Goals", string.Empty, new Vector2(220f, 20f));
        AddChildren(
            macro,
            CreateMacroClearBranch(
                ref id,
                "Continue Macro",
                CharacterMacroGoalType.Continue,
                "Clear Continue Macro",
                "Macro goal requested Continue.",
                true,
                new Vector2(120f, 120f)),
            CreateMacroTaskBranch<RunAvoidFacilityMacroGoal>(
                ref id,
                "Avoid Facility Macro",
                CharacterMacroGoalType.AvoidFacility,
                "Run Avoid Facility",
                new Vector2(120f, 220f)),
            CreateMacroTaskBranch<RunComplainMacroGoal>(
                ref id,
                "Complain Macro",
                CharacterMacroGoalType.Complain,
                "Run Complain",
                new Vector2(120f, 320f)),
            CreateMacroTaskBranch<RunVandalizeMacroGoal>(
                ref id,
                "Vandalize Macro",
                CharacterMacroGoalType.Vandalize,
                "Run Vandalize",
                new Vector2(120f, 420f)),
            CreateMacroTaskBranch<RunExitDungeonMacroGoal>(
                ref id,
                "Exit Dungeon Macro",
                CharacterMacroGoalType.ExitDungeon,
                "Run Exit Dungeon Macro",
                new Vector2(120f, 520f)),
            CreateMacroTaskBranch<RunMacroGoalDecision>(
                ref id,
                "Seek Food Macro",
                CharacterMacroGoalType.SeekFood,
                "Run Seek Food Macro",
                new Vector2(120f, 620f)),
            CreateMacroTaskBranch<RunMacroGoalDecision>(
                ref id,
                "Seek Fun Macro",
                CharacterMacroGoalType.SeekFun,
                "Run Seek Fun Macro",
                new Vector2(120f, 720f)),
            CreateMacroTaskBranch<RunMacroGoalDecision>(
                ref id,
                "Seek Toilet Macro",
                CharacterMacroGoalType.SeekToilet,
                "Run Seek Toilet Macro",
                new Vector2(120f, 820f)),
            CreateMacroTaskBranch<RunMacroGoalDecision>(
                ref id,
                "Seek Hygiene Macro",
                CharacterMacroGoalType.SeekHygiene,
                "Run Seek Hygiene Macro",
                new Vector2(120f, 920f)));

        Sequence emergency = CreateEmergencyBranch(ref id, new Vector2(500f, 20f));
        Sequence routineUtility = CreateRoutineUtilityBranch(ref id, new Vector2(820f, 20f));
        Sequence idle = CreateTopLevelIdleBranch(ref id, new Vector2(1140f, 20f));

        AddChildren(root, critical, deprivationBreakdown, lockedAction, interruptCheck, macro, emergency, routineUtility, idle);

        List<Task> detachedTasks = new List<Task>();
        source.Owner = externalBehavior;
        source.EntryTask = entry;
        source.RootTask = root;
        source.DetachedTasks = detachedTasks;
        source.Save(entry, root, detachedTasks);
        JSONSerialization.Save(source);

        externalBehavior.SetBehaviorSource(source);
        externalBehavior.Init();
        EditorUtility.SetDirty(externalBehavior);
        return true;
    }

    private static T CreateTask<T>(int id, string friendlyName, string comment, Vector2 offset)
        where T : Task, new()
    {
        T task = new T
        {
            ID = id,
            FriendlyName = friendlyName,
            NodeData = new NodeData
            {
                Offset = offset,
                FriendlyName = friendlyName,
                Comment = comment
            }
        };
        return task;
    }

    private static void AddChildren(ParentTask parent, params Task[] children)
    {
        for (int i = 0; i < children.Length; i++)
        {
            parent.AddChild(children[i], i);
        }
    }

    private static TBranch CreateJobGiverBranch<TBranch, TSelect>(
        ref int id,
        string branchName,
        string actionName,
        Vector2 branchOffset)
        where TBranch : CharacterJobGiverBranchBase, new()
        where TSelect : SelectCharacterActionBase, new()
    {
        TBranch branch = CreateTask<TBranch>(id++, branchName, string.Empty, branchOffset);
        TSelect selector = CreateTask<TSelect>(
            id++,
            $"Select {actionName}",
            string.Empty,
            branchOffset + new Vector2(-100f, 80f));
        RunSelectedCharacterAction runner = CreateTask<RunSelectedCharacterAction>(
            id++,
            $"Run {actionName}",
            string.Empty,
            branchOffset + new Vector2(100f, 80f));
        AddChildren(branch, selector, runner);
        return branch;
    }

    private static Sequence CreateLockedActionBranch(ref int id, Vector2 branchOffset)
    {
        Sequence branch = CreateTask<Sequence>(id++, "Locked Action", string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateTask<HasLockedAction>(id++, "Has Locked Action?", string.Empty, branchOffset + new Vector2(-100f, 80f)),
            CreateTask<RunLockedAction>(id++, "Run Locked Action", string.Empty, branchOffset + new Vector2(100f, 80f)));
        return branch;
    }

    private static Sequence CreateInterruptCheckBranch(ref int id, Vector2 branchOffset)
    {
        Sequence branch = CreateTask<Sequence>(id++, "Interrupt Check", string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateTask<CanInterruptCurrentAction>(id++, "Can Interrupt?", string.Empty, branchOffset + new Vector2(-100f, 80f)),
            CreateTask<StopCurrentActionForReplan>(id++, "Stop Running Action", string.Empty, branchOffset + new Vector2(100f, 80f)));
        return branch;
    }

    private static Sequence CreateEmergencyBranch(ref int id, Vector2 branchOffset)
    {
        Sequence branch = CreateTask<Sequence>(id++, "Emergency", string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateTraceTask(ref id, "Trace Emergency", CharacterAiBranch.Emergency, "긴급 후보 평가", branchOffset + new Vector2(-100f, 80f)),
            CreateTask<RunEmergencyDecision>(id++, "Run Emergency Decision", string.Empty, branchOffset + new Vector2(100f, 80f)));
        return branch;
    }

    private static Sequence CreateRoutineUtilityBranch(ref int id, Vector2 branchOffset)
    {
        Sequence branch = CreateTask<Sequence>(id++, "Routine Utility", string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateTraceTask(ref id, "Trace Routine Utility", CharacterAiBranch.RoutineUtility, "일상 후보 평가", branchOffset + new Vector2(-100f, 80f)),
            CreateTask<RunRoutineUtilityDecision>(id++, "Run Routine Utility", string.Empty, branchOffset + new Vector2(100f, 80f)));
        return branch;
    }

    private static Sequence CreateTopLevelIdleBranch(ref int id, Vector2 branchOffset)
    {
        Sequence branch = CreateTask<Sequence>(id++, "Idle", string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateTraceTask(ref id, "Trace Idle", CharacterAiBranch.Idle, "대기 행동", branchOffset + new Vector2(-100f, 80f)),
            CreateTask<RunIdleBehavior>(id++, "Run Ambient Idle", string.Empty, branchOffset + new Vector2(100f, 80f)));
        return branch;
    }

    private static RecordBtDecisionTrace CreateTraceTask(
        ref int id,
        string friendlyName,
        CharacterAiBranch branch,
        string status,
        Vector2 offset)
    {
        RecordBtDecisionTrace trace = CreateTask<RecordBtDecisionTrace>(id++, friendlyName, string.Empty, offset);
        trace.branch = branch;
        trace.status = status;
        return trace;
    }

    private static Sequence CreateContinueCurrentBranch(ref int id, Vector2 branchOffset)
    {
        Sequence branch = CreateTask<Sequence>(id++, "Continue Current", string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateTask<HasContinuableCurrentAction>(id++, "Can Continue?", string.Empty, branchOffset + new Vector2(-100f, 80f)),
            CreateTask<ContinueCurrentAction>(id++, "Keep Running Action", string.Empty, branchOffset + new Vector2(100f, 80f)));
        return branch;
    }

    private static Sequence CreateStopCurrentBranch(ref int id, Vector2 branchOffset)
    {
        Sequence branch = CreateTask<Sequence>(id++, "Stop Current", string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateTask<ShouldStopCurrentAction>(id++, "Should Stop?", string.Empty, branchOffset + new Vector2(-100f, 80f)),
            CreateTask<StopCurrentActionForReplan>(id++, "Stop Running Action", string.Empty, branchOffset + new Vector2(100f, 80f)));
        return branch;
    }

    private static SurvivalNeedsRoutineBranch CreateSurvivalNeedsRoutine(ref int id, Vector2 branchOffset)
    {
        SurvivalNeedsRoutineBranch branch =
            CreateTask<SurvivalNeedsRoutineBranch>(id++, "Survival Needs", string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateJobGiverBranch<ExitDungeonJobGiverBranch, SelectExitDungeonAction>(
                ref id,
                "Exit Dungeon",
                "Exit Dungeon Action",
                new Vector2(160f, 270f)),
            CreateJobGiverBranch<GetFoodJobGiverBranch, SelectEatAction>(
                ref id,
                "Get Food",
                "Eat Action",
                new Vector2(440f, 270f)),
            CreateJobGiverBranch<DrinkJobGiverBranch, SelectDrinkAction>(
                ref id,
                "Drink",
                "Drink Action",
                new Vector2(720f, 270f)),
            CreateJobGiverBranch<ToiletJobGiverBranch, SelectToiletAction>(
                ref id,
                "Toilet",
                "Toilet Action",
                new Vector2(1000f, 270f)),
            CreateJobGiverBranch<RestJobGiverBranch, SelectRestAction>(
                ref id,
                "Rest",
                "Rest Action",
                new Vector2(1280f, 270f)),
            CreateJobGiverBranch<HygieneJobGiverBranch, SelectHygieneAction>(
                ref id,
                "Hygiene",
                "Hygiene Action",
                new Vector2(1560f, 270f)));
        return branch;
    }

    private static DutyWorkRoutineBranch CreateDutyWorkRoutine(ref int id, Vector2 branchOffset)
    {
        DutyWorkRoutineBranch branch =
            CreateTask<DutyWorkRoutineBranch>(id++, "Duty Work", string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateJobGiverBranch<WorkJobGiverBranch, SelectWorkAction>(
                ref id,
                "Work",
                "Work Action",
                new Vector2(1480f, 430f)));
        return branch;
    }

    private static LeisureVisitRoutineBranch CreateLeisureVisitRoutine(ref int id, Vector2 branchOffset)
    {
        LeisureVisitRoutineBranch branch =
            CreateTask<LeisureVisitRoutineBranch>(id++, "Leisure Visit", string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateJobGiverBranch<ShoppingJobGiverBranch, SelectShoppingAction>(
                ref id,
                "Shopping",
                "Shopping Action",
                new Vector2(540f, 650f)),
            CreateJobGiverBranch<LookAroundJobGiverBranch, SelectLookAroundAction>(
                ref id,
                "Look Around",
                "Look Around Action",
                new Vector2(860f, 650f)));
        return branch;
    }

    private static IdleRoutineBranch CreateIdleRoutine(ref int id, Vector2 branchOffset)
    {
        IdleRoutineBranch branch =
            CreateTask<IdleRoutineBranch>(id++, "Idle Routine", string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateJobGiverBranch<WaitJobGiverBranch, SelectWaitAction>(
                ref id,
                "Wait",
                "Wait Action",
                new Vector2(1140f, 650f)),
            CreateAmbientIdleBranch(ref id, new Vector2(1460f, 650f)));
        return branch;
    }

    private static Sequence CreateAmbientIdleBranch(ref int id, Vector2 branchOffset)
    {
        AmbientIdleJobGiverBranch branch =
            CreateTask<AmbientIdleJobGiverBranch>(id++, "Ambient Idle", string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateTask<RunIdleBehavior>(id++, "Run Ambient Idle", string.Empty, branchOffset + new Vector2(0f, 80f)));
        return branch;
    }

    private static Sequence CreateMacroTaskBranch<TTask>(
        ref int id,
        string branchName,
        CharacterMacroGoalType macroGoalType,
        string taskName,
        Vector2 branchOffset)
        where TTask : Action, new()
    {
        Sequence branch = CreateTask<Sequence>(id++, branchName, string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateMacroGoalCondition(ref id, $"Has {macroGoalType}?", branchOffset + new Vector2(-100f, 80f), macroGoalType),
            CreateTask<TTask>(id++, taskName, string.Empty, branchOffset + new Vector2(100f, 80f)));
        return branch;
    }

    private static Sequence CreateMacroClearBranch(
        ref int id,
        string branchName,
        CharacterMacroGoalType macroGoalType,
        string taskName,
        string reason,
        bool failAfterClear,
        Vector2 branchOffset)
    {
        Sequence branch = CreateTask<Sequence>(id++, branchName, string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateMacroGoalCondition(ref id, $"Has {macroGoalType}?", branchOffset + new Vector2(-100f, 80f), macroGoalType),
            CreateClearMacroTask(ref id, taskName, reason, failAfterClear, branchOffset + new Vector2(100f, 80f)));
        return branch;
    }

    private static Sequence CreateMacroActionBranch<TSelect>(
        ref int id,
        string branchName,
        CharacterMacroGoalType macroGoalType,
        string actionName,
        Vector2 branchOffset)
        where TSelect : SelectCharacterActionBase, new()
    {
        Sequence branch = CreateTask<Sequence>(id++, branchName, string.Empty, branchOffset);
        AddChildren(
            branch,
            CreateMacroGoalCondition(ref id, $"Has {macroGoalType}?", branchOffset + new Vector2(-285f, 110f), macroGoalType),
            CreateTask<TSelect>(id++, $"Select {actionName} Action", string.Empty, branchOffset + new Vector2(-95f, 110f)),
            CreateTask<RunSelectedCharacterAction>(id++, $"Run {actionName} Action", string.Empty, branchOffset + new Vector2(95f, 110f)),
            CreateClearMacroTask(
                ref id,
                $"Clear {macroGoalType} Macro",
                $"{macroGoalType} macro consumed.",
                false,
                branchOffset + new Vector2(285f, 110f)));
        return branch;
    }

    private static Sequence CreateMacroUtilityBranch(
        ref int id,
        string branchName,
        CharacterMacroGoalType macroGoalType,
        Vector2 branchOffset,
        Task macroTask)
    {
        Sequence branch = CreateTask<Sequence>(id++, branchName, string.Empty, branchOffset);
        SetNodeOffset(macroTask, branchOffset + new Vector2(100f, 100f));
        AddChildren(
            branch,
            CreateMacroGoalCondition(ref id, $"Has {macroGoalType}?", branchOffset + new Vector2(-100f, 100f), macroGoalType),
            macroTask);
        return branch;
    }

    private static void SetNodeOffset(Task task, Vector2 offset)
    {
        if (task == null)
        {
            return;
        }

        task.NodeData ??= new NodeData();
        task.NodeData.Offset = offset;
    }

    private static HasMacroGoalType CreateMacroGoalCondition(
        ref int id,
        string friendlyName,
        Vector2 offset,
        CharacterMacroGoalType macroGoalType)
    {
        HasMacroGoalType condition = CreateTask<HasMacroGoalType>(id++, friendlyName, string.Empty, offset);
        condition.goalType = macroGoalType;
        return condition;
    }

    private static ClearMacroGoal CreateClearMacroTask(
        ref int id,
        string friendlyName,
        string reason,
        bool failAfterClear)
    {
        return CreateClearMacroTask(
            ref id,
            friendlyName,
            reason,
            failAfterClear,
            new Vector2(150f, 180f));
    }

    private static ClearMacroGoal CreateClearMacroTask(
        ref int id,
        string friendlyName,
        string reason,
        bool failAfterClear,
        Vector2 offset)
    {
        ClearMacroGoal task = CreateTask<ClearMacroGoal>(id++, friendlyName, string.Empty, offset);
        task.reason = reason;
        task.failAfterClear = failAfterClear;
        return task;
    }

    private static void EnsureExternalBehaviorFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Behavior Designer"))
        {
            AssetDatabase.CreateFolder("Assets", "Behavior Designer");
        }

        if (!AssetDatabase.IsValidFolder(ExternalBehaviorFolder))
        {
            AssetDatabase.CreateFolder("Assets/Behavior Designer", "External Behaviors");
        }
    }

    private static bool EnsureBehaviorTreeUsesExternalGraph(
        BehaviorTree tree,
        ExternalBehaviorTree externalBehavior)
    {
        if (tree == null || externalBehavior == null)
        {
            return false;
        }

        bool changed = ReferenceEquals(tree.ExternalBehavior, null)
            || tree.ExternalBehavior.GetInstanceID() != externalBehavior.GetInstanceID();
        if (changed)
        {
            tree.ExternalBehavior = externalBehavior;
        }

        BehaviorSource source = tree.GetBehaviorSource();
        if (source == null
            || !ReferenceEquals(source.Owner, tree)
            || source.EntryTask != null
            || source.RootTask != null
            || source.DetachedTasks != null
            || source.TaskData != null)
        {
            tree.SetBehaviorSource(new BehaviorSource(tree)
            {
                behaviorName = "Character AI",
                behaviorDescription = string.Empty
            });
            changed = true;
        }
        else
        {
            if (source.behaviorName != "Character AI")
            {
                source.behaviorName = "Character AI";
                changed = true;
            }

            if (!string.IsNullOrEmpty(source.behaviorDescription))
            {
                source.behaviorDescription = string.Empty;
                changed = true;
            }

            if (!ReferenceEquals(source.Owner, tree))
            {
                source.Owner = tree;
            }
        }

        if (tree.StartWhenEnabled)
        {
            tree.StartWhenEnabled = false;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(tree);
        }

        return changed;
    }

    private static int BuildPrefabGraphs(ExternalBehaviorTree externalBehavior)
    {
        int changes = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (CharacterActor actor in root.GetComponentsInChildren<CharacterActor>(true))
                {
                    changed |= EnsureActorGraph(actor, externalBehavior);
                }

                foreach (CharacterAiScheduler scheduler in root.GetComponentsInChildren<CharacterAiScheduler>(true))
                {
                    changed |= EnsureSchedulerGraph(scheduler, externalBehavior);
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changes++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return changes;
    }

    private static int BuildOpenSceneGraphs(ExternalBehaviorTree externalBehavior)
    {
        int changes = 0;
        HashSet<Scene> changedScenes = new HashSet<Scene>();
        foreach (CharacterActor actor in Object.FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            if (EnsureActorGraph(actor, externalBehavior))
            {
                EditorUtility.SetDirty(actor.gameObject);
                changedScenes.Add(actor.gameObject.scene);
                changes++;
            }
        }

        foreach (CharacterAiScheduler scheduler in Object.FindObjectsByType<CharacterAiScheduler>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            if (EnsureSchedulerGraph(scheduler, externalBehavior))
            {
                EditorUtility.SetDirty(scheduler);
                changedScenes.Add(scheduler.gameObject.scene);
                changes++;
            }
        }

        foreach (Scene scene in changedScenes)
        {
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        return changes;
    }

    private static int BuildCurrentPrefabStageGraph(ExternalBehaviorTree externalBehavior)
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null || stage.prefabContentsRoot == null)
        {
            return 0;
        }

        int changes = 0;
        foreach (CharacterActor actor in stage.prefabContentsRoot.GetComponentsInChildren<CharacterActor>(true))
        {
            if (EnsureActorGraph(actor, externalBehavior))
            {
                EditorUtility.SetDirty(actor.gameObject);
                changes++;
            }
        }

        foreach (CharacterAiScheduler scheduler in stage.prefabContentsRoot.GetComponentsInChildren<CharacterAiScheduler>(true))
        {
            if (EnsureSchedulerGraph(scheduler, externalBehavior))
            {
                EditorUtility.SetDirty(scheduler);
                changes++;
            }
        }

        if (changes > 0 && !string.IsNullOrEmpty(stage.assetPath))
        {
            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);
            EditorSceneManager.MarkSceneDirty(stage.scene);
        }

        return changes;
    }

    private static int BuildRuntimeGraphs(ExternalBehaviorTree externalBehavior)
    {
        int changes = 0;
        foreach (CharacterActor actor in Object.FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            if (EnsureActorGraph(actor, externalBehavior))
            {
                changes++;
            }

            BehaviorTree tree = actor != null ? actor.GetComponent<BehaviorTree>() : null;
            if (tree != null)
            {
                tree.DungeonStoryRefreshVisualStatus(actor);
            }
        }

        foreach (CharacterAiScheduler scheduler in Object.FindObjectsByType<CharacterAiScheduler>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            if (EnsureSchedulerGraph(scheduler, externalBehavior))
            {
                changes++;
            }
        }

        return changes;
    }

    private static bool EnsureActorGraph(CharacterActor actor, ExternalBehaviorTree externalBehavior)
    {
        BehaviorTree tree = actor.GetComponent<BehaviorTree>();
        if (tree == null)
        {
            tree = actor.gameObject.AddComponent<BehaviorTree>();
        }

        return EnsureBehaviorTreeUsesExternalGraph(tree, externalBehavior);
    }

    private static bool EnsureSchedulerGraph(
        CharacterAiScheduler scheduler,
        ExternalBehaviorTree externalBehavior)
    {
        if (scheduler == null || externalBehavior == null)
        {
            return false;
        }

        SerializedObject serialized = new SerializedObject(scheduler);
        SerializedProperty externalProperty = serialized.FindProperty("characterAiExternalBehavior");
        if (externalProperty == null || externalProperty.objectReferenceValue == externalBehavior)
        {
            return false;
        }

        externalProperty.objectReferenceValue = externalBehavior;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static void RefreshOpenBehaviorDesignerWindow()
    {
        BehaviorDesignerWindow window = BehaviorDesignerWindow.instance;
        BehaviorSource activeSource = window != null ? window.ActiveBehaviorSource : null;
        if (activeSource == null)
        {
            return;
        }

        try
        {
            BehaviorSource refreshedSource = activeSource.Owner != null
                ? activeSource.Owner.GetBehaviorSource()
                : activeSource;
            if (EditorApplication.isPlaying
                && activeSource.Owner is BehaviorTree tree
                && tree.ExecutionStatus != TaskStatus.Running
                && tree.ExternalBehavior is ExternalBehaviorTree externalBehavior
                && externalBehavior.BehaviorSource != null)
            {
                refreshedSource = externalBehavior.BehaviorSource;
            }

            window.LoadBehavior(refreshedSource, false, false);
            window.Repaint();
        }
        catch (System.Exception exception)
        {
            Debug.Log($"Recovered open Behavior Designer window refresh: {exception.Message}");
        }
    }
}
