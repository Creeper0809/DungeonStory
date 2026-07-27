using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace BehaviorDesigner.Runtime
{
    // Wrapper for the Behavior class
    [AddComponentMenu("Behavior Designer/Behavior Tree")]
    public class BehaviorTree : Behavior
    {
        // DungeonStory patch: CharacterAiScheduler drives this wrapper manually so character AI
        // can be budgeted, staggered, and debugged without BehaviorManager's automatic frame tick.
        [SerializeField] private bool useDungeonStoryManualRuntime = true;
        [SerializeField] private bool alsoTickSerializedBehaviorGraph;
        [SerializeField] private string dungeonStoryBranch;
        [SerializeField] private string dungeonStoryTask;
        [SerializeField] private string dungeonStoryStatus;
        [SerializeField] private int dungeonStoryTickCount;
        private readonly System.Collections.Generic.Dictionary<Task, GameObject> dungeonStoryBoundTaskGameObjects =
            new System.Collections.Generic.Dictionary<Task, GameObject>();
        private Task dungeonStoryRuntimeRoot;
        private GameObject dungeonStoryRuntimeActor;
        private bool dungeonStoryRuntimeTreeAwake;

        public bool UseDungeonStoryManualRuntime => useDungeonStoryManualRuntime;
        public string DungeonStoryBranch => dungeonStoryBranch;
        public string DungeonStoryTask => dungeonStoryTask;
        public string DungeonStoryStatus => dungeonStoryStatus;
        public int DungeonStoryTickCount => dungeonStoryTickCount;

        public void DungeonStoryRecordDirectTick(
            global::CharacterActor actor,
            global::CharacterAiDecisionTickResult result)
        {
            dungeonStoryTickCount++;
            global::CharacterBlackboard blackboard = actor != null
                ? actor.Blackboard
                : null;
            dungeonStoryBranch = result.Branch.ToString();
            dungeonStoryTask = result.Task;
            dungeonStoryStatus = result.Status;
            if (blackboard != null)
            {
                dungeonStoryBranch = blackboard.CurrentBranch.ToString();
                dungeonStoryTask = blackboard.CurrentTask;
                dungeonStoryStatus = blackboard.CurrentStatus;
            }
        }

        public void DungeonStoryEnsureBehaviorSourceOwnership()
        {
            BehaviorSource source = GetBehaviorSource();
            if (source == null)
            {
                SetBehaviorSource(new BehaviorSource(this));
                return;
            }

            if (!ReferenceEquals(source.Owner, this))
            {
                source.Owner = this;
            }
        }

        public void DungeonStoryReloadExternalBehaviorForRuntime()
        {
            if (Application.isPlaying && ExecutionStatus == TaskStatus.Running)
            {
                DisableBehavior();
            }

            dungeonStoryRuntimeRoot = null;
            dungeonStoryRuntimeActor = null;
            dungeonStoryRuntimeTreeAwake = false;
            dungeonStoryBoundTaskGameObjects.Clear();
            dungeonStoryBranch = global::CharacterAiBranch.None.ToString();
            dungeonStoryTask = string.Empty;
            dungeonStoryStatus = "Runtime graph reloaded.";
            SetBehaviorSource(new BehaviorSource(this)
            {
                behaviorName = "Character AI",
                behaviorDescription = string.Empty
            });
        }

        public void DungeonStoryRefreshVisualStatus(global::CharacterActor actor)
        {
            if (!useDungeonStoryManualRuntime || actor == null)
            {
                return;
            }

            actor.EnsureRuntimeState();
            if (!TryPrepareDungeonStoryRuntimeTree(actor, out Task rootTask, out _))
            {
                return;
            }

            RefreshDungeonStoryVisualPath(actor, rootTask);
        }

        public bool DungeonStoryManualTick(global::CharacterActor actor)
        {
            dungeonStoryTickCount++;

            if (!useDungeonStoryManualRuntime)
            {
                dungeonStoryBranch = "BehaviorDesignerGraph";
                dungeonStoryTask = "ManualTick";
                dungeonStoryStatus = "DungeonStory runtime disabled; graph tick only.";
                return true;
            }

            if (actor == null)
            {
                dungeonStoryBranch = "None";
                dungeonStoryTask = "Prepare";
                dungeonStoryStatus = "CharacterActor is missing.";
                Debug.LogError($"{name}: CharacterActor is required for DungeonStory BehaviorTree manual tick.", this);
                return false;
            }

            actor.EnsureRuntimeState();
            if (!TryPrepareDungeonStoryRuntimeTree(actor, out Task rootTask, out string error))
            {
                dungeonStoryBranch = global::CharacterAiBranch.None.ToString();
                dungeonStoryTask = "BehaviorTree";
                dungeonStoryStatus = error;
                actor.Blackboard?.RecordBtStatus(
                    global::CharacterAiBranch.None,
                    dungeonStoryTask,
                    dungeonStoryStatus);
                Debug.LogError($"{name}: {error}", this);
                return false;
            }

            actor.Blackboard?.BeginDecisionTrace(dungeonStoryTickCount);
            actor.Blackboard?.ClearJobGiverCandidateCache();
            TaskStatus status = EvaluateDungeonStoryTask(rootTask);
            if (actor.Blackboard != null)
            {
                dungeonStoryBranch = actor.Blackboard.CurrentBranch.ToString();
                dungeonStoryTask = actor.Blackboard.CurrentTask;
                dungeonStoryStatus = actor.Blackboard.CurrentStatus;
            }
            else
            {
                dungeonStoryBranch = global::CharacterAiBranch.None.ToString();
                dungeonStoryTask = rootTask.FriendlyName;
                dungeonStoryStatus = status.ToString();
            }

            return status == TaskStatus.Success || status == TaskStatus.Running;
        }

        private void RefreshDungeonStoryVisualPath(global::CharacterActor actor, Task rootTask)
        {
            float now = Time.realtimeSinceStartup;
            ClearDungeonStoryExecutionStatus(rootTask, now);
            MarkDungeonStoryNodeRunning(rootTask, now);

            global::CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
            if (blackboard == null)
            {
                return;
            }

            switch (blackboard.CurrentBranch)
            {
                case global::CharacterAiBranch.Critical:
                    MarkDungeonStoryPathWithDescendants(rootTask, now, "Critical");
                    break;
                case global::CharacterAiBranch.MacroGoal:
                    MarkDungeonStoryMacroPath(rootTask, blackboard, now);
                    break;
                case global::CharacterAiBranch.ContinueCurrent:
                    MarkDungeonStoryPathWithDescendants(rootTask, now, "Continue Current", "Can Continue?", "Keep Running Action");
                    break;
                case global::CharacterAiBranch.StopCurrent:
                    MarkDungeonStoryPathWithDescendants(rootTask, now, "Stop Current", "Should Stop?", "Stop Running Action");
                    break;
                case global::CharacterAiBranch.ExitDungeon:
                    MarkDungeonStoryActionPath(rootTask, blackboard, now, "Survival Needs", "Exit Dungeon", "Exit Dungeon Action");
                    break;
                case global::CharacterAiBranch.Eat:
                    MarkDungeonStoryActionPath(rootTask, blackboard, now, "Survival Needs", "Get Food", "Eat Action");
                    break;
                case global::CharacterAiBranch.Rest:
                    MarkDungeonStoryActionPath(rootTask, blackboard, now, "Survival Needs", "Rest", "Rest Action");
                    break;
                case global::CharacterAiBranch.Work:
                    MarkDungeonStoryActionPath(rootTask, blackboard, now, "Duty Work", "Work", "Work Action");
                    break;
                case global::CharacterAiBranch.Shopping:
                    MarkDungeonStoryActionPath(rootTask, blackboard, now, "Leisure Visit", "Shopping", "Shopping Action");
                    break;
                case global::CharacterAiBranch.LookAround:
                    MarkDungeonStoryActionPath(rootTask, blackboard, now, "Leisure Visit", "Look Around", "Look Around Action");
                    break;
                case global::CharacterAiBranch.Wait:
                    MarkDungeonStoryActionPath(rootTask, blackboard, now, "Idle Routine", "Wait", "Wait Action");
                    break;
                case global::CharacterAiBranch.Idle:
                    MarkDungeonStoryPathWithDescendants(rootTask, now, "Normal Routine", "Idle Routine", "Ambient Idle", "Run Ambient Idle");
                    break;
            }
        }

        private void MarkDungeonStoryMacroPath(Task rootTask, global::CharacterBlackboard blackboard, float now)
        {
            string task = blackboard != null ? blackboard.CurrentTask : string.Empty;
            string branchName = task switch
            {
                "ContinueMacro" => "Continue Macro",
                "AvoidFacility" => "Avoid Facility Macro",
                "Complain" => "Complain Macro",
                "Vandalize" => "Vandalize Macro",
                "ExitDungeon" => "Exit Dungeon Macro",
                _ when task != null && task.Contains("Seek Food") => "Seek Food Macro",
                _ when task != null && task.Contains("Seek Fun") => "Seek Fun Macro",
                _ => string.Empty
            };

            string leafName = task switch
            {
                "ContinueMacro" => "Clear Continue Macro",
                "AvoidFacility" => "Run Avoid Facility",
                "Complain" => "Run Complain",
                "Vandalize" => "Run Vandalize",
                "ExitDungeon" => "Run Exit Dungeon Macro",
                _ when task != null && task.Contains("Seek Food") => "Run Seek Food Macro",
                _ when task != null && task.Contains("Seek Fun") => "Run Seek Fun Macro",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(branchName) && !string.IsNullOrEmpty(leafName))
            {
                MarkDungeonStoryPathWithDescendants(rootTask, now, "Macro Goals", branchName, leafName);
                return;
            }

            MarkDungeonStoryPathWithDescendants(rootTask, now, "Macro Goals");
        }

        private static void MarkDungeonStoryActionPath(
            Task rootTask,
            global::CharacterBlackboard blackboard,
            float now,
            string groupName,
            string branchName,
            string actionName)
        {
            string leafPrefix = blackboard != null
                && blackboard.CurrentTask != null
                && blackboard.CurrentTask.StartsWith("Select ")
                ? "Select "
                : "Run ";
            MarkDungeonStoryPathWithDescendants(
                rootTask,
                now,
                "Normal Routine",
                groupName,
                branchName,
                leafPrefix + actionName);
        }

        private static void ClearDungeonStoryExecutionStatus(Task task, float now)
        {
            if (task == null)
            {
                return;
            }

            if (task.NodeData != null)
            {
                if (task.NodeData.ExecutionStatus == TaskStatus.Running)
                {
                    task.NodeData.PopTime = now;
                }

                task.NodeData.ExecutionStatus = TaskStatus.Inactive;
                task.NodeData.IsReevaluating = false;
            }

            if (task is not ParentTask parent || parent.Children == null)
            {
                return;
            }

            foreach (Task child in parent.Children)
            {
                ClearDungeonStoryExecutionStatus(child, now);
            }
        }

        private static bool MarkDungeonStoryPath(Task rootTask, float now, params string[] friendlyNames)
        {
            return MarkDungeonStoryPathAndGetLast(rootTask, now, out _, friendlyNames);
        }

        private static bool MarkDungeonStoryPathWithDescendants(Task rootTask, float now, params string[] friendlyNames)
        {
            if (!MarkDungeonStoryPathAndGetLast(rootTask, now, out Task finalTask, friendlyNames))
            {
                return false;
            }

            MarkDungeonStorySubtreeRunning(finalTask, now);
            return true;
        }

        private static bool MarkDungeonStoryPathAndGetLast(
            Task rootTask,
            float now,
            out Task finalTask,
            params string[] friendlyNames)
        {
            finalTask = rootTask;
            for (int i = 0; i < friendlyNames.Length; i++)
            {
                finalTask = FindDungeonStoryChild(finalTask, friendlyNames[i]);
                if (finalTask == null)
                {
                    return false;
                }

                MarkDungeonStoryNodeRunning(finalTask, now);
            }

            return true;
        }

        private static Task FindDungeonStoryChild(Task task, string friendlyName)
        {
            if (task is not ParentTask parent || parent.Children == null)
            {
                return null;
            }

            foreach (Task child in parent.Children)
            {
                if (child != null && child.FriendlyName == friendlyName)
                {
                    return child;
                }
            }

            return null;
        }

        private static void MarkDungeonStoryNodeRunning(Task task, float now)
        {
            if (task != null && task.NodeData != null)
            {
                task.NodeData.ExecutionStatus = TaskStatus.Running;
                task.NodeData.PushTime = now;
                task.NodeData.PopTime = 0f;
                task.NodeData.InterruptTime = 0f;
            }
        }

        private static void MarkDungeonStorySubtreeRunning(Task task, float now)
        {
            if (task == null)
            {
                return;
            }

            MarkDungeonStoryNodeRunning(task, now);
            if (task is not ParentTask parent || parent.Children == null)
            {
                return;
            }

            foreach (Task child in parent.Children)
            {
                MarkDungeonStorySubtreeRunning(child, now);
            }
        }

        private bool TryPrepareDungeonStoryRuntimeTree(
            global::CharacterActor actor,
            out Task rootTask,
            out string error)
        {
            rootTask = null;
            error = string.Empty;

            if (ExternalBehavior == null)
            {
                error = "External Behavior Tree is missing.";
                return false;
            }

            // BehaviorSource.Owner is not serialized. CharacterActor.OnEnable can ask the
            // scheduler to prepare this tree before Behavior Designer has restored ownership.
            DungeonStoryEnsureBehaviorSourceOwnership();
            BehaviorSource source = GetBehaviorSource();

            if (dungeonStoryRuntimeTreeAwake
                && dungeonStoryRuntimeRoot != null
                && dungeonStoryRuntimeActor == actor.gameObject
                && source != null
                && source.RootTask == dungeonStoryRuntimeRoot)
            {
                rootTask = dungeonStoryRuntimeRoot;
                return true;
            }

            bool needsRuntimeLoad = source.RootTask == null;
            try
            {
                CheckForSerialization(needsRuntimeLoad, true);
            }
            catch (System.Exception exception)
            {
                string actorName = actor != null ? actor.name : "<missing actor>";
                string behaviorName = ExternalBehavior != null ? ExternalBehavior.name : "<missing external behavior>";
                throw new System.InvalidOperationException(
                    $"Failed to prepare Behavior Designer tree '{behaviorName}' for '{actorName}'.",
                    exception);
            }

            source = GetBehaviorSource();
            if (source != null && !ReferenceEquals(source.Owner, this))
            {
                source.Owner = this;
            }

            rootTask = source != null ? source.RootTask : null;
            if (rootTask == null)
            {
                error = "External Behavior Tree has no runtime root task.";
                return false;
            }

            bool rebuildAwake = !ReferenceEquals(dungeonStoryRuntimeRoot, rootTask);
            dungeonStoryRuntimeRoot = rootTask;
            BindDungeonStoryTaskTree(rootTask, actor, rebuildAwake || !dungeonStoryRuntimeTreeAwake);
            dungeonStoryRuntimeActor = actor.gameObject;
            dungeonStoryRuntimeTreeAwake = true;
            return true;
        }

        private void BindDungeonStoryTaskTree(Task task, global::CharacterActor actor, bool callAwake)
        {
            if (task == null)
            {
                return;
            }

            bool actorChanged = !dungeonStoryBoundTaskGameObjects.TryGetValue(task, out GameObject boundGameObject)
                || boundGameObject != actor.gameObject;
            task.Owner = this;
            task.GameObject = actor.gameObject;
            task.Transform = actor.transform;
            dungeonStoryBoundTaskGameObjects[task] = actor.gameObject;
            // DungeonStory patch: external behavior tasks can be reused across actors,
            // so cached task components must be rebound whenever the actor changes.
            if (callAwake || actorChanged)
            {
                task.OnAwake();
            }

            if (task is not ParentTask parent || parent.Children == null)
            {
                return;
            }

            foreach (Task child in parent.Children)
            {
                BindDungeonStoryTaskTree(child, actor, callAwake);
            }
        }

        private TaskStatus EvaluateDungeonStoryTask(Task task)
        {
            if (task == null || task.Disabled)
            {
                return TaskStatus.Failure;
            }

            task.OnStart();
            TaskStatus status = task is ParentTask parent
                ? EvaluateDungeonStoryParentTask(parent)
                : task.OnUpdate();
            if (status != TaskStatus.Running)
            {
                task.OnEnd();
            }

            return status;
        }

        private TaskStatus EvaluateDungeonStoryParentTask(ParentTask parent)
        {
            if (parent.Children == null || parent.Children.Count == 0)
            {
                return TaskStatus.Failure;
            }

            if (parent is not Selector
                && parent is not Sequence
                && parent is not UtilitySelector
                && parent is not PrioritySelector)
            {
                dungeonStoryBranch = global::CharacterAiBranch.None.ToString();
                dungeonStoryTask = parent.FriendlyName;
                dungeonStoryStatus = $"Unsupported DungeonStory manual BT parent: {parent.GetType().Name}.";
                return TaskStatus.Failure;
            }

            TaskStatus status = TaskStatus.Failure;
            int safety = parent.Children.Count + 1;
            while (parent.CanExecute() && safety-- > 0)
            {
                int childIndex = parent.CurrentChildIndex();
                if (childIndex < 0 || childIndex >= parent.Children.Count)
                {
                    status = TaskStatus.Failure;
                    break;
                }

                parent.OnChildStarted(childIndex);
                parent.OnChildStarted();
                status = EvaluateDungeonStoryTask(parent.Children[childIndex]);
                parent.OnChildExecuted(childIndex, status);
                parent.OnChildExecuted(status);
                if (status == TaskStatus.Running)
                {
                    break;
                }
            }

            return parent.OverrideStatus(parent.Decorate(status));
        }
    }
}
