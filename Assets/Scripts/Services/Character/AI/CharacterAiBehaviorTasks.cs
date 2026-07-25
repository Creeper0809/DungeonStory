using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace BehaviorDesigner.Runtime.Tasks.DungeonStory
{
    internal static class CharacterAiBehaviorTaskServices
    {
        public static bool TryGetDecisionPipeline(
            CharacterActor actor,
            out ICharacterAiDecisionPipeline decisionPipeline)
        {
            decisionPipeline = null;
            if (actor == null || actor.Brain == null)
            {
                return false;
            }

            decisionPipeline = actor.Brain.RequireDecisionPipeline();
            return true;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class HasCriticalState : Conditional
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            return actor == null
                || CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline)
                && decisionPipeline.HasCriticalState(actor)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class HasMacroGoal : Conditional
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            return CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline)
                && decisionPipeline.HasMacroGoal(actor)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class HasDeprivationBreakdown : Conditional
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            return CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline)
                && decisionPipeline.HasDeprivationBreakdown(actor)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class RunDeprivationBreakdown : Action
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            return decisionPipeline.RunDeprivationBreakdown(actor).Handled
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class HasLockedAction : Conditional
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            return CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline)
                && decisionPipeline.HasLockedAction(actor)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class CanInterruptCurrentAction : Conditional
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            return CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline)
                && decisionPipeline.CanInterruptCurrentAction(actor)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class RunLockedAction : Action
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiDecisionTickResult result =
                decisionPipeline.RunLockedAction(actor);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class RunEmergencyDecision : Action
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiDecisionTickResult result =
                decisionPipeline.RunEmergencyDecision(actor);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class RunRoutineUtilityDecision : Action
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiDecisionTickResult result =
                decisionPipeline.RunRoutineUtilityDecision(actor);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class RecordBtDecisionTrace : Action
    {
        public CharacterAiBranch branch = CharacterAiBranch.None;
        public string status = "enter";
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiDecisionTickResult result =
                decisionPipeline.RecordBtDecisionTrace(actor, branch, FriendlyName, status);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class HasMacroGoalType : Conditional
    {
        public CharacterMacroGoalType goalType;
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            return CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline)
                && decisionPipeline.HasMacroGoalType(actor, goalType)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class ClearMacroGoal : Action
    {
        public string reason = "Macro goal consumed.";
        public bool failAfterClear;
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
            if (blackboard == null || !blackboard.HasActiveMacroGoal())
            {
                return TaskStatus.Failure;
            }

            blackboard.ClearMacroGoal(reason);
            return failAfterClear ? TaskStatus.Failure : TaskStatus.Success;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class RunComplainMacroGoal : Action
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiDecisionTickResult result = decisionPipeline.RunComplainMacro(
                actor,
                blackboard,
                blackboard != null ? blackboard.ActiveMacroGoal : null);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class RunAvoidFacilityMacroGoal : Action
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiDecisionTickResult result = decisionPipeline.ApplyAvoidFacility(
                actor,
                blackboard,
                blackboard != null ? blackboard.ActiveMacroGoal : null);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class RunExitDungeonMacroGoal : Action
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiDecisionTickResult result = decisionPipeline.RunExitDungeonMacro(
                actor,
                blackboard,
                blackboard != null ? blackboard.ActiveMacroGoal : null);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class RunVandalizeMacroGoal : Action
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiDecisionTickResult result = decisionPipeline.RunVandalizeMacro(
                actor,
                blackboard,
                blackboard != null ? blackboard.ActiveMacroGoal : null);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class RunMacroGoalDecision : Action
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiDecisionTickResult result =
                decisionPipeline.RunMacroGoalDecision(actor);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class RunCriticalState : Action
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiDecisionTickResult result =
                decisionPipeline.RunCritical(actor, actor != null ? actor.Blackboard : null);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class HasContinuableCurrentAction : Conditional
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            return CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline)
                && decisionPipeline.HasContinuableCurrentAction(actor)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class ContinueCurrentAction : Action
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiDecisionTickResult result =
                decisionPipeline.ContinueCurrentAction(actor);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class ShouldStopCurrentAction : Conditional
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            return CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline)
                && decisionPipeline.ShouldStopCurrentActionForReplan(actor)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class StopCurrentActionForReplan : Action
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiDecisionTickResult result =
                decisionPipeline.StopCurrentActionForReplan(actor);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    public abstract class CharacterRoutineGroupBranchBase : UtilitySelector
    {
        private CharacterActor actor;

        protected abstract CharacterAiBranch RoutineBranch { get; }

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override float GetPriority()
        {
            return EvaluatePriority();
        }

        public override float GetUtility()
        {
            return EvaluatePriority() / 100f;
        }

        private float EvaluatePriority()
        {
            if (actor == null)
            {
                actor = GetComponent<CharacterActor>();
            }

            float priority = CharacterAiRoutinePriority.GetPriority(
                actor,
                RoutineBranch,
                out string reason);
            actor?.Blackboard?.RecordRoutineGroupPriority(RoutineBranch, priority, reason);
            return priority;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class SurvivalNeedsRoutineBranch : CharacterRoutineGroupBranchBase
    {
        protected override CharacterAiBranch RoutineBranch => CharacterAiBranch.SurvivalNeeds;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class DutyWorkRoutineBranch : CharacterRoutineGroupBranchBase
    {
        protected override CharacterAiBranch RoutineBranch => CharacterAiBranch.DutyWork;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class LeisureVisitRoutineBranch : CharacterRoutineGroupBranchBase
    {
        protected override CharacterAiBranch RoutineBranch => CharacterAiBranch.LeisureVisit;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class IdleRoutineBranch : CharacterRoutineGroupBranchBase
    {
        protected override CharacterAiBranch RoutineBranch => CharacterAiBranch.Idle;
    }

    public abstract class CharacterJobGiverBranchBase : Sequence
    {
        private CharacterActor actor;

        protected abstract CharacterAiBranch Branch { get; }

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override float GetUtility()
        {
            if (actor == null)
            {
                actor = GetComponent<CharacterActor>();
            }

            CharacterAiJobGiver jobGiver = ResolveJobGiver();
            if (actor == null || actor.Brain == null || jobGiver == null)
            {
                return 0f;
            }

            bool hasUtility = jobGiver.TryEvaluate(actor, out CharacterAiJobCandidate candidate);
            actor.Blackboard?.RecordJobGiverUtility(
                jobGiver.Branch,
                hasUtility ? candidate.Utility : 0f,
                candidate.DebugSummary);
            if (hasUtility)
            {
                actor.Blackboard?.CacheJobGiverCandidate(candidate);
            }
            else
            {
                actor.Blackboard?.RemoveJobGiverCandidateCache(jobGiver.Branch);
            }

            return hasUtility ? candidate.Utility : 0f;
        }

        private CharacterAiJobGiver ResolveJobGiver()
        {
            if (actor == null || actor.Brain == null)
            {
                return null;
            }

            return actor.Brain.RequireJobGiverCatalog().Get(Branch);
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class ExitDungeonJobGiverBranch : CharacterJobGiverBranchBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.ExitDungeon;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class GetFoodJobGiverBranch : CharacterJobGiverBranchBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.Eat;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class RestJobGiverBranch : CharacterJobGiverBranchBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.Rest;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class ToiletJobGiverBranch : CharacterJobGiverBranchBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.Toilet;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class HygieneJobGiverBranch : CharacterJobGiverBranchBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.Hygiene;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class WorkJobGiverBranch : CharacterJobGiverBranchBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.Work;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class ShoppingJobGiverBranch : CharacterJobGiverBranchBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.Shopping;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class LookAroundJobGiverBranch : CharacterJobGiverBranchBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.LookAround;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class WaitJobGiverBranch : CharacterJobGiverBranchBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.Wait;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class AmbientIdleJobGiverBranch : Sequence
    {
        private const float IdleUtility = 0.001f;
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override float GetUtility()
        {
            if (actor == null)
            {
                actor = GetComponent<CharacterActor>();
            }

            if (actor == null || !actor.CanRunAi)
            {
                return 0f;
            }

            actor.Blackboard?.RecordJobGiverUtility(
                CharacterAiBranch.Idle,
                IdleUtility,
                "No higher utility job giver selected.");
            return IdleUtility;
        }
    }

    public abstract class SelectCharacterActionBase : Action
    {
        private CharacterActor actor;

        protected abstract CharacterAiBranch Branch { get; }

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiJobGiver jobGiver = actor != null && actor.Brain != null
                ? actor.Brain.RequireJobGiverCatalog().Get(Branch)
                : null;
            CharacterAiDecisionTickResult result = decisionPipeline.SelectJobGiverAction(
                actor,
                jobGiver,
                FriendlyName);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class SelectExitDungeonAction : SelectCharacterActionBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.ExitDungeon;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class SelectEatAction : SelectCharacterActionBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.Eat;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class SelectRestAction : SelectCharacterActionBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.Rest;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class SelectToiletAction : SelectCharacterActionBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.Toilet;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class SelectHygieneAction : SelectCharacterActionBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.Hygiene;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class SelectWorkAction : SelectCharacterActionBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.Work;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class SelectShoppingAction : SelectCharacterActionBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.Shopping;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class SelectLookAroundAction : SelectCharacterActionBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.LookAround;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class SelectWaitAction : SelectCharacterActionBase
    {
        protected override CharacterAiBranch Branch => CharacterAiBranch.Wait;
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class RunSelectedCharacterAction : Action
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiDecisionTickResult result =
                decisionPipeline.RunSelectedAction(actor, FriendlyName);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class RunIdleBehavior : Action
    {
        private CharacterActor actor;

        public override void OnAwake()
        {
            actor = GetComponent<CharacterActor>();
        }

        public override TaskStatus OnUpdate()
        {
            if (!CharacterAiBehaviorTaskServices.TryGetDecisionPipeline(actor, out ICharacterAiDecisionPipeline decisionPipeline))
            {
                return TaskStatus.Failure;
            }

            CharacterAiDecisionTickResult result = decisionPipeline.RunIdleBehavior(actor, actor != null ? actor.Blackboard : null);
            return result.Handled ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("DungeonStory/Character AI")]
    public sealed class EmitContextBubble : Action
    {
        public string line = "AI context";
        private CharacterDialogueRuntime dialogueRuntime;

        public override void OnAwake()
        {
            dialogueRuntime = GetComponent<CharacterDialogueRuntime>();
        }

        public override TaskStatus OnUpdate()
        {
            if (dialogueRuntime == null)
            {
                return TaskStatus.Failure;
            }

            dialogueRuntime.ShowLine(line);
            return TaskStatus.Success;
        }
    }
}
