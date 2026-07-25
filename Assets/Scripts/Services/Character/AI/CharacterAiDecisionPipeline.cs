using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct CharacterAiDecisionTickResult
{
    public CharacterAiDecisionTickResult(
        bool handled,
        CharacterAiBranch branch,
        string task,
        string status)
    {
        Handled = handled;
        Branch = branch;
        Task = task ?? string.Empty;
        Status = status ?? string.Empty;
    }

    public bool Handled { get; }
    public CharacterAiBranch Branch { get; }
    public string Task { get; }
    public string Status { get; }
}

public interface ICharacterAiDecisionPipeline
{
    bool HasCriticalState(CharacterActor actor);
    CharacterAiDecisionTickResult RunCritical(CharacterActor actor, CharacterBlackboard blackboard);
    bool HasDeprivationBreakdown(CharacterActor actor);
    CharacterAiDecisionTickResult RunDeprivationBreakdown(CharacterActor actor);
    bool HasLockedAction(CharacterActor actor);
    bool CanInterruptCurrentAction(CharacterActor actor);
    CharacterAiDecisionTickResult RunLockedAction(CharacterActor actor);
    bool HasMacroGoal(CharacterActor actor);
    bool HasContinuableCurrentAction(CharacterActor actor);
    bool ShouldStopCurrentActionForReplan(CharacterActor actor);
    CharacterAiDecisionTickResult ContinueCurrentAction(CharacterActor actor);
    CharacterAiDecisionTickResult StopCurrentActionForReplan(CharacterActor actor);
    CharacterAiDecisionTickResult SelectJobGiverAction(CharacterActor actor, CharacterAiJobGiver jobGiver, string taskName);
    CharacterAiDecisionTickResult RunSelectedAction(
        CharacterActor actor,
        string taskName,
        CharacterAiBranch branchOverride = CharacterAiBranch.None);
    CharacterAiDecisionTickResult RunMacroGoalDecision(CharacterActor actor);
    CharacterAiDecisionTickResult RunEmergencyDecision(CharacterActor actor);
    CharacterAiDecisionTickResult RunRoutineUtilityDecision(CharacterActor actor);
    CharacterAiDecisionTickResult RunIdleBehavior(CharacterActor actor, CharacterBlackboard blackboard);
    CharacterAiDecisionTickResult RecordBtDecisionTrace(CharacterActor actor, CharacterAiBranch branch, string taskName, string status);
    bool HasMacroGoalType(CharacterActor actor, CharacterMacroGoalType goalType);
    CharacterAiDecisionTickResult ClearContinueMacro(CharacterActor actor);
    CharacterAiDecisionTickResult RunComplainMacro(CharacterActor actor, CharacterBlackboard blackboard, CharacterMacroGoal goal);
    CharacterAiDecisionTickResult ApplyAvoidFacility(CharacterActor actor, CharacterBlackboard blackboard, CharacterMacroGoal goal);
    CharacterAiDecisionTickResult RunExitDungeonMacro(CharacterActor actor, CharacterBlackboard blackboard, CharacterMacroGoal goal);
    CharacterAiDecisionTickResult RunVandalizeMacro(CharacterActor actor, CharacterBlackboard blackboard, CharacterMacroGoal goal);
}

public interface ICharacterAiFacilityLookup
{
    BuildableObject FindFacility(int id, string tag);
}

public sealed class CharacterAiFacilityLookup : ICharacterAiFacilityLookup
{
    private readonly IBuildingWorldQuery buildingWorld;

    public CharacterAiFacilityLookup(IBuildingWorldQuery buildingWorld)
    {
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
    }

    public BuildableObject FindFacility(int id, string tag)
    {
        IReadOnlyList<BuildableObject> buildings = buildingWorld.Buildings;
        foreach (BuildableObject building in buildings)
        {
            if (CharacterAiDecisionPipeline.MatchesFacility(building, id, tag))
            {
                return building;
            }
        }

        return null;
    }
}

public sealed class CharacterAiDecisionPipeline : ICharacterAiDecisionPipeline
{
    private const float SecondaryEmergencyNeedThreshold = 0.2f;
    private readonly ICharacterDeprivationRuntime deprivationRuntime;

    public CharacterAiDecisionPipeline(
        ICharacterDeprivationRuntime deprivationRuntime = null)
    {
        this.deprivationRuntime = deprivationRuntime;
    }

    public bool HasCriticalState(CharacterActor actor)
    {
        return actor == null
            || actor.IsDead
            || (actor.Brain != null && actor.Brain.IsManualCommandActive)
            || actor.CurrentLifecycleState == CharacterLifecycleState.ExitingDungeon
            || actor.CurrentLifecycleState == CharacterLifecycleState.PreparingExpedition
            || actor.CurrentLifecycleState == CharacterLifecycleState.DepartingExpedition
            || actor.CurrentLifecycleState == CharacterLifecycleState.ReturningExpedition
            || actor.CurrentLifecycleState == CharacterLifecycleState.Despawned
            || actor.CurrentLifecycleState == CharacterLifecycleState.OnExpedition;
    }

    public CharacterAiDecisionTickResult RunCritical(CharacterActor actor, CharacterBlackboard blackboard)
    {
        if (actor == null)
        {
            return Result(false, CharacterAiBranch.Critical, "HasCriticalState", "Actor is missing.", blackboard);
        }

        if (actor.Brain != null && actor.Brain.IsManualCommandActive)
        {
            return Result(
                true,
                CharacterAiBranch.Critical,
                "PlayerMoveCommand",
                actor.Brain.CurrentActionPhaseDetail,
                blackboard);
        }

        blackboard.ClearCommitment(CharacterAiInterruptReason.Critical, actor.CurrentLifecycleState.ToString());
        if (actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = false;
        }

        return Result(true, CharacterAiBranch.Critical, "HasCriticalState", actor.CurrentLifecycleState.ToString(), blackboard);
    }

    public bool HasMacroGoal(CharacterActor actor)
    {
        return actor != null && actor.Blackboard != null && actor.Blackboard.HasActiveMacroGoal();
    }

    public bool HasDeprivationBreakdown(CharacterActor actor)
    {
        return deprivationRuntime?.HasActiveBreakdown(actor) == true;
    }

    public CharacterAiDecisionTickResult RunDeprivationBreakdown(CharacterActor actor)
    {
        CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
        if (deprivationRuntime == null
            || !deprivationRuntime.TryRunActiveBreakdown(actor, out string status))
        {
            return Result(false, CharacterAiBranch.DeprivationBreakdown, "Run Deprivation Breakdown", "활성 붕괴 없음", blackboard);
        }

        blackboard?.SetIntent(
            CharacterAiBranch.DeprivationBreakdown,
            status,
            "Run Deprivation Breakdown",
            status);
        actor.Brain?.BeginExternallyDrivenAction(
            "결핍 붕괴",
            status,
            "붕괴 행동이 끝날 때까지 유지");
        return Result(true, CharacterAiBranch.DeprivationBreakdown, "Run Deprivation Breakdown", status, blackboard);
    }

    public bool HasLockedAction(CharacterActor actor)
    {
        return actor != null
            && actor.Brain != null
            && actor.Brain.CanContinueCurrentAction(out _)
            && !actor.Brain.ShouldStopCurrentActionForReplan(out _);
    }

    public bool CanInterruptCurrentAction(CharacterActor actor)
    {
        return actor != null
            && actor.Brain != null
            && actor.Brain.ShouldStopCurrentActionForReplan(out _);
    }

    public bool HasContinuableCurrentAction(CharacterActor actor)
    {
        return actor != null
            && actor.Brain != null
            && actor.Brain.CanContinueCurrentAction(out _);
    }

    public bool ShouldStopCurrentActionForReplan(CharacterActor actor)
    {
        return actor != null
            && actor.Brain != null
            && actor.Brain.ShouldStopCurrentActionForReplan(out _);
    }

    public CharacterAiDecisionTickResult RunLockedAction(CharacterActor actor)
    {
        if (!TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return Result(false, CharacterAiBranch.LockedAction, "Locked Action", error, blackboard);
        }

        if (actor.Brain == null)
        {
            return Result(false, CharacterAiBranch.LockedAction, "Locked Action", "AIBrain is missing.", blackboard);
        }

        if (actor.Brain.ShouldStopCurrentActionForReplan(out string stopReason))
        {
            return Result(false, CharacterAiBranch.LockedAction, "Locked Action", stopReason, blackboard);
        }

        if (!actor.Brain.CanContinueCurrentAction(out string status))
        {
            return Result(false, CharacterAiBranch.LockedAction, "Locked Action", status, blackboard);
        }

        AIAction runningAction = actor.Brain.bestAction;
        actor.Brain.isBestActionEnd = false;
        runningAction?.actionset?.RefreshDestinationReservation(actor, runningAction.destination);
        blackboard.RefreshCommitment(runningAction);
        blackboard.SetIntent(
            CharacterAiBranch.LockedAction,
            GetActionLabel(runningAction?.actionset),
            "Locked Action",
            status);
        actor.AiMemory?.RecordDecision(
            CharacterAiBranch.LockedAction,
            CharacterAiUtilityText.GetIntention(runningAction?.actionset?.Branch ?? CharacterAiBranch.None),
            status,
            0.05f);
        return Result(true, CharacterAiBranch.LockedAction, "Locked Action", status, blackboard);
    }

    public CharacterAiDecisionTickResult ContinueCurrentAction(CharacterActor actor)
    {
        if (!TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return Result(false, CharacterAiBranch.ContinueCurrent, "Continue Current Action", error, blackboard);
        }

        if (actor.Brain == null)
        {
            return Result(false, CharacterAiBranch.ContinueCurrent, "Continue Current Action", "AIBrain is missing.", blackboard);
        }

        if (!actor.Brain.CanContinueCurrentAction(out string status))
        {
            return Result(false, CharacterAiBranch.ContinueCurrent, "Continue Current Action", status, blackboard);
        }

        AIAction runningAction = actor.Brain.bestAction;
        actor.Brain.isBestActionEnd = false;
        runningAction?.actionset?.RefreshDestinationReservation(actor, runningAction.destination);
        blackboard.RefreshCommitment(runningAction);
        blackboard.SetIntent(
            CharacterAiBranch.ContinueCurrent,
            GetActionLabel(runningAction?.actionset),
            "Continue Current Action",
            status);
        return Result(true, CharacterAiBranch.ContinueCurrent, "Continue Current Action", status, blackboard);
    }

    public CharacterAiDecisionTickResult StopCurrentActionForReplan(CharacterActor actor)
    {
        if (!TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return Result(false, CharacterAiBranch.InterruptCheck, "Interrupt Check", error, blackboard);
        }

        if (actor.Brain == null)
        {
            return Result(false, CharacterAiBranch.InterruptCheck, "Interrupt Check", "AIBrain is missing.", blackboard);
        }

        if (!actor.Brain.ShouldStopCurrentActionForReplan(out string stopReason))
        {
            return Result(false, CharacterAiBranch.InterruptCheck, "Interrupt Check", "Current action does not need replan.", blackboard);
        }

        AIAction runningAction = actor.Brain.bestAction;
        string actionLabel = GetActionLabel(runningAction?.actionset);
        actor.Brain.StopCurrentActionForReplan(stopReason);
        blackboard.ForceClearCommitment(CharacterAiInterruptReason.CurrentActionStopped, stopReason);
        blackboard.SetIntent(
            CharacterAiBranch.InterruptCheck,
            actionLabel,
            "Interrupt Check",
            stopReason);
        return Result(true, CharacterAiBranch.InterruptCheck, "Interrupt Check", stopReason, blackboard);
    }

    public CharacterAiDecisionTickResult SelectJobGiverAction(
        CharacterActor actor,
        CharacterAiJobGiver jobGiver,
        string taskName)
    {
        CharacterAiBranch branch = jobGiver != null
            ? jobGiver.Branch
            : CharacterAiBranch.None;
        if (!TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return Result(false, branch, taskName, error, blackboard);
        }

        if (actor.Brain == null)
        {
            return Result(false, branch, taskName, "AIBrain is missing.", blackboard);
        }

        if (jobGiver == null)
        {
            return Result(false, branch, taskName, "JobGiver is missing.", blackboard);
        }

        if (!blackboard.TryGetCachedJobGiverCandidate(branch, out CharacterAiJobCandidate jobCandidate)
            && !jobGiver.TryEvaluate(actor, out jobCandidate))
        {
            return Result(false, branch, taskName, jobCandidate.DebugSummary, blackboard);
        }

        blackboard.RecordSelectedJobGiverUtility(jobCandidate);
        if (!actor.Brain.TryCommitActionCandidate(jobCandidate.ActionCandidate, out AIActionFailure failure))
        {
            blackboard.ReportActionFailure(null, failure);
            return Result(false, branch, taskName, failure.ToString(), blackboard);
        }

        AIAction selectedAction = actor.Brain.bestAction;
        string actionLabel = GetActionLabel(selectedAction?.actionset);
        string destinationLabel = GetBuildingLabel(selectedAction?.destination);
        string status = $"{destinationLabel} | {jobCandidate.DebugSummary}";
        blackboard.SetIntent(branch, actionLabel, taskName, status);
        return Result(true, branch, taskName, status, blackboard);
    }

    public CharacterAiDecisionTickResult RunSelectedAction(
        CharacterActor actor,
        string taskName,
        CharacterAiBranch branchOverride = CharacterAiBranch.None)
    {
        if (!TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return Result(false, CharacterAiBranch.None, taskName, error, blackboard);
        }

        AIAction selectedAction = actor.Brain != null ? actor.Brain.bestAction : null;
        CharacterAiBranch branch = branchOverride != CharacterAiBranch.None
            ? branchOverride
            : GetBranchForActionSet(selectedAction?.actionset);
        if (selectedAction == null || selectedAction.actionset == null)
        {
            return Result(false, branch, taskName, "No selected AI action.", blackboard);
        }

        bool executed = actor.TryExecuteSelectedAiAction();
        if (executed)
        {
            blackboard.RefreshCommitment(selectedAction);
        }

        return Result(
            executed,
            branch,
            taskName,
            executed ? GetActionLabel(selectedAction.actionset) : "Selected action could not execute.",
            blackboard);
    }

    public CharacterAiDecisionTickResult RunMacroGoalDecision(CharacterActor actor)
    {
        if (!TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return Result(false, CharacterAiBranch.MacroGoal, "Run Macro Goal", error, blackboard);
        }

        CharacterMacroGoal goal = blackboard.ActiveMacroGoal;
        if (goal == null || !blackboard.HasActiveMacroGoal())
        {
            return Result(false, CharacterAiBranch.MacroGoal, "Run Macro Goal", "No active macro goal.", blackboard);
        }

        return goal.type switch
        {
            CharacterMacroGoalType.Continue => ClearContinueMacro(actor),
            CharacterMacroGoalType.SeekFood => RunMacroJobGiverDecision(
                actor,
                blackboard,
                goal,
                "Seek Food",
                RequireJobGiverCatalog(actor).GetFood),
            CharacterMacroGoalType.SeekToilet => RunMacroJobGiverDecision(
                actor,
                blackboard,
                goal,
                "Seek Toilet",
                RequireJobGiverCatalog(actor).Toilet),
            CharacterMacroGoalType.SeekHygiene => RunMacroJobGiverDecision(
                actor,
                blackboard,
                goal,
                "Seek Hygiene",
                RequireJobGiverCatalog(actor).Hygiene),
            CharacterMacroGoalType.SeekFun => RunMacroJobGiverDecision(
                actor,
                blackboard,
                goal,
                "Seek Fun",
                RequireJobGiverCatalog(actor).Shopping,
                RequireJobGiverCatalog(actor).LookAround),
            CharacterMacroGoalType.AvoidFacility => ApplyAvoidFacility(
                actor,
                blackboard,
                goal),
            CharacterMacroGoalType.Complain => RunComplainMacro(
                actor,
                blackboard,
                goal),
            CharacterMacroGoalType.ExitDungeon => RunExitDungeonMacro(
                actor,
                blackboard,
                goal),
            CharacterMacroGoalType.Vandalize => RunVandalizeMacro(
                actor,
                blackboard,
                goal),
            _ => Result(false, CharacterAiBranch.MacroGoal, "Run Macro Goal", $"Unsupported macro goal: {goal.type}.", blackboard)
        };
    }

    public CharacterAiDecisionTickResult RunIdleBehavior(CharacterActor actor, CharacterBlackboard blackboard)
    {
        if (!TryPrepare(actor, out blackboard, out string error))
        {
            return Result(false, CharacterAiBranch.Idle, "RunIdleBehavior", error, blackboard);
        }

        if (!actor.TryGetAbility(out AbilityMove _))
        {
            return Result(false, CharacterAiBranch.Idle, "RunIdleBehavior", "AbilityMove is missing.", blackboard);
        }

        if (actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = false;
        }

        if (IdleBehaviorRunner.TryRunDefault(actor, 1.0f, true, out string behaviorName, out string failureReason))
        {
            actor.Brain?.ClearSelectedActionForIdle(behaviorName);
            blackboard.ClearCommitment(CharacterAiInterruptReason.ManualReplan, "Idle behavior selected.");
            blackboard.RecordSelectedUtilitySummary($"AmbientIdle utility=explicit behavior={behaviorName}");
            blackboard.SetIntent(CharacterAiBranch.Idle, behaviorName, "RunIdleBehavior", behaviorName);
            actor.AddActivity(CharacterActivityEvent.InternalAi(
                CharacterActivityOutcomes.Started,
                "idle-selected",
                $"AI idle: {behaviorName}"));
            return Result(true, CharacterAiBranch.Idle, "RunIdleBehavior", behaviorName, blackboard);
        }

        actor.AddActivity(CharacterActivityEvent.InternalAi(
            CharacterActivityOutcomes.Failed,
            "idle-failed",
            $"AI idle failed: {failureReason}"));
        if (actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
        }

        return Result(false, CharacterAiBranch.Idle, "RunIdleBehavior", failureReason, blackboard);
    }

    public CharacterAiDecisionTickResult RunEmergencyDecision(CharacterActor actor)
    {
        if (!TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return Result(false, CharacterAiBranch.Emergency, "Run Emergency Decision", error, blackboard);
        }

        if (HasDeprivationBreakdown(actor))
        {
            return RunDeprivationBreakdown(actor);
        }

        if (deprivationRuntime != null
            && deprivationRuntime.TryRunSafeEmergencyRelief(actor, out string reliefStatus))
        {
            blackboard.SetIntent(
                CharacterAiBranch.Emergency,
                reliefStatus,
                "Run Safe Emergency Relief",
                reliefStatus);
            return Result(true, CharacterAiBranch.Emergency, "Run Safe Emergency Relief", reliefStatus, blackboard);
        }

        CharacterAiDecisionContext context = CharacterAiDecisionContext.Capture(actor, CharacterAiBranch.Emergency);
        blackboard.RecordDecisionContext(context);
        if (context.EmergencyScore < 0.58f)
        {
            return Result(
                false,
                CharacterAiBranch.Emergency,
                "Run Emergency Decision",
                $"긴급도 낮음 {context.EmergencyScore * 100f:0}%",
                blackboard);
        }

        List<CharacterAiJobGiver> emergencyGivers = BuildEmergencyJobGivers(actor, context);
        return TrySelectAndRunBestJobGiver(
            actor,
            blackboard,
            CharacterAiBranch.Emergency,
            "Run Emergency Decision",
            emergencyGivers,
            branch => 1f,
            out _);
    }

    public CharacterAiDecisionTickResult RunRoutineUtilityDecision(CharacterActor actor)
    {
        if (!TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return Result(false, CharacterAiBranch.RoutineUtility, "Run Routine Utility", error, blackboard);
        }

        CharacterAiDecisionContext context = CharacterAiDecisionContext.Capture(actor, CharacterAiBranch.RoutineUtility);
        blackboard.RecordDecisionContext(context);

        Dictionary<CharacterAiBranch, float> groupPriorities = new Dictionary<CharacterAiBranch, float>
        {
            [CharacterAiBranch.SurvivalNeeds] = CharacterAiRoutinePriority.GetPriority(actor, CharacterAiBranch.SurvivalNeeds, out string survivalReason),
            [CharacterAiBranch.DutyWork] = CharacterAiRoutinePriority.GetPriority(actor, CharacterAiBranch.DutyWork, out string dutyReason),
            [CharacterAiBranch.LeisureVisit] = CharacterAiRoutinePriority.GetPriority(actor, CharacterAiBranch.LeisureVisit, out string leisureReason),
            [CharacterAiBranch.Idle] = CharacterAiRoutinePriority.GetPriority(actor, CharacterAiBranch.Idle, out string idleReason)
        };
        blackboard.RecordRoutineGroupPriority(CharacterAiBranch.SurvivalNeeds, groupPriorities[CharacterAiBranch.SurvivalNeeds], survivalReason);
        blackboard.RecordRoutineGroupPriority(CharacterAiBranch.DutyWork, groupPriorities[CharacterAiBranch.DutyWork], dutyReason);
        blackboard.RecordRoutineGroupPriority(CharacterAiBranch.LeisureVisit, groupPriorities[CharacterAiBranch.LeisureVisit], leisureReason);
        blackboard.RecordRoutineGroupPriority(CharacterAiBranch.Idle, groupPriorities[CharacterAiBranch.Idle], idleReason);

        CharacterAiDecisionTickResult result = TrySelectAndRunBestJobGiver(
            actor,
            blackboard,
            CharacterAiBranch.RoutineUtility,
            "Run Routine Utility",
            BuildRoutineJobGivers(actor),
            branch => GetGroupPriorityMultiplier(branch, groupPriorities),
            out string failure);
        if (result.Handled)
        {
            return result;
        }

        return Result(false, CharacterAiBranch.RoutineUtility, "Run Routine Utility", failure, blackboard);
    }

    public CharacterAiDecisionTickResult RecordBtDecisionTrace(
        CharacterActor actor,
        CharacterAiBranch branch,
        string taskName,
        string status)
    {
        CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
        blackboard?.RecordBtDecisionTrace(taskName, status);
        return Result(actor != null, branch, taskName, status, blackboard);
    }

    public bool HasMacroGoalType(CharacterActor actor, CharacterMacroGoalType goalType)
    {
        CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
        return blackboard != null
            && blackboard.HasActiveMacroGoal()
            && blackboard.ActiveMacroGoal != null
            && blackboard.ActiveMacroGoal.type == goalType;
    }

    private CharacterAiDecisionTickResult TrySelectAndRunBestJobGiver(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterAiBranch branchOverride,
        string taskName,
        IEnumerable<CharacterAiJobGiver> jobGivers,
        Func<CharacterAiBranch, float> branchMultiplier,
        out string failureSummary)
    {
        failureSummary = "No utility candidates.";
        if (actor == null || actor.Brain == null)
        {
            failureSummary = "AIBrain is missing.";
            return Result(false, branchOverride, taskName, failureSummary, blackboard);
        }

        CharacterAiJobCandidate bestCandidate = default;
        float bestAdjustedUtility = float.MinValue;
        bool hasCandidate = false;
        List<string> rejected = new List<string>();
        foreach (CharacterAiJobGiver jobGiver in jobGivers ?? Array.Empty<CharacterAiJobGiver>())
        {
            if (jobGiver == null)
            {
                continue;
            }

            if (!jobGiver.TryEvaluate(actor, out CharacterAiJobCandidate candidate))
            {
                rejected.Add(candidate.DebugSummary);
                blackboard.RecordJobGiverUtility(jobGiver.Branch, 0f, candidate.DebugSummary);
                continue;
            }

            float multiplier = branchMultiplier != null
                ? Mathf.Max(0f, branchMultiplier(jobGiver.Branch))
                : 1f;
            float adjusted = Mathf.Clamp01(candidate.Utility * multiplier);
            adjusted = CharacterMoodImpulseUtility.ApplyFinalAutonomyBias(
                actor,
                candidate.Branch,
                adjusted,
                candidate.ActionCandidate.Action);
            blackboard.RecordJobGiverUtility(
                jobGiver.Branch,
                adjusted,
                $"{candidate.DebugSummary} group={multiplier:0.##}");
            if (!hasCandidate || adjusted > bestAdjustedUtility)
            {
                bestCandidate = candidate;
                bestAdjustedUtility = adjusted;
                hasCandidate = true;
            }
        }

        if (!hasCandidate)
        {
            failureSummary = rejected.Count > 0
                ? string.Join(" / ", rejected.Take(3))
                : "No valid utility candidates.";
            return Result(false, branchOverride, taskName, failureSummary, blackboard);
        }

        blackboard.RecordSelectedJobGiverUtility(bestCandidate);
        if (!actor.Brain.TryCommitActionCandidate(bestCandidate.ActionCandidate, out AIActionFailure failure))
        {
            blackboard.ReportActionFailure(null, failure);
            failureSummary = failure.ToString();
            return Result(false, branchOverride, taskName, failureSummary, blackboard);
        }

        CharacterAiDecisionTickResult runResult = RunSelectedAction(actor, taskName, branchOverride);
        AIAction selectedAction = actor.Brain.bestAction;
        string actionLabel = GetActionLabel(selectedAction?.actionset);
        string status = $"{actionLabel} · {bestCandidate.DebugSummary}";
        actor.AiMemory?.RecordDecision(
            branchOverride,
            CharacterAiUtilityText.GetIntention(bestCandidate.Branch),
            status,
            0.1f);
        return Result(runResult.Handled, branchOverride, taskName, status, blackboard);
    }

    private static List<CharacterAiJobGiver> BuildEmergencyJobGivers(
        CharacterActor actor,
        CharacterAiDecisionContext context)
    {
        ICharacterAiJobGiverCatalog catalog = RequireJobGiverCatalog(actor);
        List<CharacterAiJobGiver> result = new List<CharacterAiJobGiver>();
        if (CharacterNeedCatalog.GetWeightedUrgency(actor, CharacterCondition.HUNGER)
            >= SecondaryEmergencyNeedThreshold)
        {
            result.Add(catalog.GetFood);
        }

        if (CharacterNeedCatalog.GetWeightedUrgency(actor, CharacterCondition.EXCRETION)
            >= SecondaryEmergencyNeedThreshold)
        {
            result.Add(catalog.Toilet);
        }

        if (CharacterNeedCatalog.GetWeightedUrgency(actor, CharacterCondition.HYGIENE)
            >= SecondaryEmergencyNeedThreshold)
        {
            result.Add(catalog.Hygiene);
        }

        if (CharacterNeedCatalog.GetWeightedUrgency(actor, CharacterCondition.SLEEP)
                >= SecondaryEmergencyNeedThreshold
            || context.InjuryUrgency > 0.35f
            || context.HealthUrgency > 0.45f)
        {
            result.Add(catalog.Rest);
        }

        if (CharacterWorkRoleUtility.TryGetWork(actor, out _)
            && (context.FoodStockPressure > 0.65f || context.WaterStockPressure > 0.65f))
        {
            result.Add(catalog.Work);
        }

        if (!CharacterWorkRoleUtility.TryGetWork(actor, out _))
        {
            result.Add(catalog.ExitDungeon);
        }

        result.Add(catalog.Wait);
        return result.Distinct().Where(giver => giver != null).ToList();
    }

    private static List<CharacterAiJobGiver> BuildRoutineJobGivers(CharacterActor actor)
    {
        ICharacterAiJobGiverCatalog catalog = RequireJobGiverCatalog(actor);
        List<CharacterAiJobGiver> result = new List<CharacterAiJobGiver>
        {
            catalog.GetFood,
            catalog.Toilet,
            catalog.Hygiene,
            catalog.Rest
        };

        if (CharacterWorkRoleUtility.TryGetWork(actor, out _))
        {
            result.Add(catalog.Work);
            result.Add(catalog.Wait);
        }
        else
        {
            result.Add(catalog.Shopping);
            result.Add(catalog.LookAround);
            result.Add(catalog.ExitDungeon);
        }

        return result.Where(giver => giver != null).ToList();
    }

    private static float GetGroupPriorityMultiplier(
        CharacterAiBranch jobBranch,
        IReadOnlyDictionary<CharacterAiBranch, float> groupPriorities)
    {
        CharacterAiBranch group = jobBranch switch
        {
            CharacterAiBranch.Eat => CharacterAiBranch.SurvivalNeeds,
            CharacterAiBranch.Rest => CharacterAiBranch.SurvivalNeeds,
            CharacterAiBranch.Toilet => CharacterAiBranch.SurvivalNeeds,
            CharacterAiBranch.Hygiene => CharacterAiBranch.SurvivalNeeds,
            CharacterAiBranch.ExitDungeon => CharacterAiBranch.SurvivalNeeds,
            CharacterAiBranch.Work => CharacterAiBranch.DutyWork,
            CharacterAiBranch.Shopping => CharacterAiBranch.LeisureVisit,
            CharacterAiBranch.LookAround => CharacterAiBranch.LeisureVisit,
            CharacterAiBranch.Wait => CharacterAiBranch.Idle,
            _ => CharacterAiBranch.Idle
        };
        float priority = groupPriorities != null && groupPriorities.TryGetValue(group, out float value)
            ? value
            : 1f;
        return Mathf.Lerp(0.45f, 1.2f, Mathf.Clamp01(priority / 100f));
    }

    private CharacterAiDecisionTickResult RunMacroJobGiverDecision(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal,
        string label,
        params CharacterAiJobGiver[] jobGivers)
    {
        string taskName = $"Macro {label} JobGiver";
        if (!TryPrepare(actor, out blackboard, out string error))
        {
            return Result(false, CharacterAiBranch.MacroGoal, taskName, error, blackboard);
        }

        if (goal == null || !blackboard.HasActiveMacroGoal())
        {
            return Result(false, CharacterAiBranch.MacroGoal, taskName, "Macro goal is missing.", blackboard);
        }

        CharacterAiJobCandidate bestCandidate = default;
        bool hasCandidate = false;
        string lastFailure = "No JobGiver candidates.";
        if (jobGivers != null)
        {
            foreach (CharacterAiJobGiver jobGiver in jobGivers)
            {
                if (jobGiver == null)
                {
                    continue;
                }

                if (jobGiver.TryEvaluate(actor, out CharacterAiJobCandidate candidate))
                {
                    if (!hasCandidate || candidate.Utility > bestCandidate.Utility)
                    {
                        bestCandidate = candidate;
                        hasCandidate = true;
                    }

                    continue;
                }

                lastFailure = candidate.DebugSummary;
            }
        }

        if (!hasCandidate)
        {
            blackboard.ClearMacroGoal($"{label} macro could not find a JobGiver candidate: {lastFailure}");
            return Result(false, CharacterAiBranch.MacroGoal, taskName, lastFailure, blackboard);
        }

        blackboard.RecordSelectedJobGiverUtility(bestCandidate);
        if (!actor.Brain.TryCommitActionCandidate(bestCandidate.ActionCandidate, out AIActionFailure failure))
        {
            blackboard.ReportActionFailure(null, failure);
            blackboard.ClearMacroGoal($"{label} macro could not commit candidate: {failure}");
            return Result(false, CharacterAiBranch.MacroGoal, taskName, failure.ToString(), blackboard);
        }

        CharacterAiDecisionTickResult runResult = RunSelectedAction(
            actor,
            $"Run {label} Macro Action",
            CharacterAiBranch.MacroGoal);
        string status = $"{runResult.Status} | {bestCandidate.DebugSummary}";
        if (runResult.Handled)
        {
            string reason = goal != null && !string.IsNullOrWhiteSpace(goal.reason)
                ? goal.reason
                : $"{label} macro consumed.";
            blackboard.ClearMacroGoal(reason);
        }
        else
        {
            blackboard.ClearMacroGoal($"{label} macro action failed: {runResult.Status}");
        }

        return Result(
            runResult.Handled,
            CharacterAiBranch.MacroGoal,
            $"Run {label} Macro Action",
            status,
            blackboard);
    }

    public CharacterAiDecisionTickResult ClearContinueMacro(CharacterActor actor)
    {
        CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
        if (blackboard == null || !blackboard.HasActiveMacroGoal())
        {
            return Result(false, CharacterAiBranch.MacroGoal, "ContinueMacro", "No active macro goal.", blackboard);
        }

        blackboard.ClearMacroGoal("Macro goal requested Continue.");
        return Result(false, CharacterAiBranch.MacroGoal, "ContinueMacro", "Continue.", blackboard);
    }

    public CharacterAiDecisionTickResult RunComplainMacro(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal)
    {
        if (!TryPrepare(actor, out blackboard, out string error))
        {
            return Result(false, CharacterAiBranch.MacroGoal, "Complain", error, blackboard);
        }

        goal ??= blackboard.ActiveMacroGoal;
        if (goal == null)
        {
            return Result(false, CharacterAiBranch.MacroGoal, "Complain", "Macro goal is missing.", blackboard);
        }

        actor.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Social,
            CharacterActivityOutcomes.Responded,
            $"불만을 털어놓았다: {goal.reason}",
            actionId: "macro:complain",
            reasonCode: goal.reason,
            sentiment: -0.65f,
            bubbleEligible: true));
        blackboard.ClearMacroGoal("Complain emitted.");
        return Result(true, CharacterAiBranch.MacroGoal, "Complain", "Complain.", blackboard);
    }

    public CharacterAiDecisionTickResult ApplyAvoidFacility(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal)
    {
        if (!TryPrepare(actor, out blackboard, out string error))
        {
            return Result(false, CharacterAiBranch.MacroGoal, "AvoidFacility", error, blackboard);
        }

        if (goal == null)
        {
            return Result(false, CharacterAiBranch.MacroGoal, "AvoidFacility", "Macro goal is missing.", blackboard);
        }

        BuildableObject target = FindFacility(actor, goal.targetFacilityId, goal.targetFacilityTag);
        if (target == null)
        {
            blackboard.ClearMacroGoal("AvoidFacility target not found.");
            return Result(false, CharacterAiBranch.MacroGoal, "AvoidFacility", "Target facility not found.", blackboard);
        }

        blackboard.PutFacilityOnCooldown(target, goal.reason);
        blackboard.ClearMacroGoal("AvoidFacility cooldown applied.");
        return Result(true, CharacterAiBranch.MacroGoal, "AvoidFacility", target.name, blackboard);
    }

    public CharacterAiDecisionTickResult RunExitDungeonMacro(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal)
    {
        if (!TryPrepare(actor, out blackboard, out string error))
        {
            return Result(false, CharacterAiBranch.MacroGoal, "ExitDungeon", error, blackboard);
        }

        if (goal == null)
        {
            return Result(false, CharacterAiBranch.MacroGoal, "ExitDungeon", "Macro goal is missing.", blackboard);
        }

        if (!actor.TryGetAbility(out AbilityMove move))
        {
            return Result(false, CharacterAiBranch.MacroGoal, "ExitDungeon", "AbilityMove is missing.", blackboard);
        }

        if (CharacterWorkRoleUtility.TryGetWork(actor, out _))
        {
            blackboard.ClearMacroGoal("Workers cannot exit through ordinary mood macros.");
            return Result(false, CharacterAiBranch.MacroGoal, "ExitDungeon", "Worker exit is handled by staff systems.", blackboard);
        }

        actor.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Lifecycle,
            CharacterActivityOutcomes.Departed,
            $"던전을 떠나기로 했다: {goal.reason}",
            actionId: "macro:exit-dungeon",
            reasonCode: goal.reason,
            sentiment: -0.8f,
            bubbleEligible: true));
        blackboard.ClearCommitment(CharacterAiInterruptReason.MacroGoalChanged, "ExitDungeon macro.");
        blackboard.ClearMacroGoal("ExitDungeon started.");
        if (actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = false;
        }

        move.StartExitDungeon();
        return Result(true, CharacterAiBranch.MacroGoal, "ExitDungeon", "Exit started.", blackboard);
    }

    public CharacterAiDecisionTickResult RunVandalizeMacro(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal)
    {
        if (!TryPrepare(actor, out blackboard, out string error))
        {
            return Result(false, CharacterAiBranch.MacroGoal, "Vandalize", error, blackboard);
        }

        if (goal == null)
        {
            return Result(false, CharacterAiBranch.MacroGoal, "Vandalize", "Macro goal is missing.", blackboard);
        }

        BuildableObject target = FindFacility(actor, goal.targetFacilityId, goal.targetFacilityTag);
        if (target == null)
        {
            blackboard.ClearMacroGoal("Vandalize target not found.");
            actor.AddActivity(CharacterActivityEvent.InternalAi(
                CharacterActivityOutcomes.Failed,
                "vandalize-target-missing",
                $"AI macro vandalize failed: target not found - {goal.reason}"));
            return Result(false, CharacterAiBranch.MacroGoal, "Vandalize", "Target facility not found.", blackboard);
        }

        if (!CanVandalize(target, out string failureReason))
        {
            blackboard.ClearMacroGoal($"Vandalize target rejected: {failureReason}");
            actor.AddActivity(CharacterActivityEvent.InternalAi(
                CharacterActivityOutcomes.Failed,
                "vandalize-target-rejected",
                $"AI macro vandalize failed: {failureReason}"));
            return Result(false, CharacterAiBranch.MacroGoal, "Vandalize", failureReason, blackboard);
        }

        target.SetDamaged(true);
        blackboard.ClearCommitment(CharacterAiInterruptReason.MacroGoalChanged, "Vandalize macro executed.");
        blackboard.ClearMacroGoal("Vandalize completed.");
        actor.AddActivity(CharacterActivityEvent.Facility(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Damaged,
            $"{GetBuildingLabel(target)}을 파손했다",
            target,
            actionId: "macro:vandalize",
            reasonCode: goal.reason,
            value: 1f,
            bubbleEligible: true));
        return Result(true, CharacterAiBranch.MacroGoal, "Vandalize", GetBuildingLabel(target), blackboard);
    }

    private static BuildableObject FindFacility(CharacterActor actor, int id, string tag)
    {
        return RequireFacilityLookup(actor).FindFacility(id, tag);
    }

    public static bool MatchesFacility(BuildableObject building, int id, string tag)
    {
        if (building == null)
        {
            return false;
        }

        if (id >= 0 && building.id == id)
        {
            return true;
        }

        return building.HasSemanticTag(tag);
    }

    private static ICharacterAiFacilityLookup RequireFacilityLookup(CharacterActor actor)
    {
        if (actor == null || actor.Brain == null)
        {
            throw new InvalidOperationException(
                $"{nameof(CharacterAiDecisionPipeline)} requires an actor with {nameof(AIBrain)} for facility lookup.");
        }

        return actor.Brain.RequireFacilityLookup();
    }

    private static ICharacterAiJobGiverCatalog RequireJobGiverCatalog(CharacterActor actor)
    {
        if (actor == null || actor.Brain == null)
        {
            throw new InvalidOperationException(
                $"{nameof(CharacterAiDecisionPipeline)} requires an actor with {nameof(AIBrain)} for job giver lookup.");
        }

        return actor.Brain.RequireJobGiverCatalog();
    }

    private static bool CanVandalize(BuildableObject target, out string failureReason)
    {
        failureReason = string.Empty;
        if (target == null)
        {
            failureReason = "Target facility is missing.";
            return false;
        }

        if (target.isDestroy)
        {
            failureReason = "Target facility is destroyed.";
            return false;
        }

        if (target.IsDamaged)
        {
            failureReason = "Target facility is already damaged.";
            return false;
        }

        if (target.IsGridMovement)
        {
            failureReason = "Movement buildings cannot be vandalized.";
            return false;
        }

        if (target.Facility == null)
        {
            failureReason = "Target is not a facility.";
            return false;
        }

        return true;
    }

    private static string GetBuildingLabel(BuildableObject building)
    {
        if (building == null)
        {
            return "None";
        }

        return building.BuildingData != null && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
            ? building.BuildingData.objectName
            : building.name;
    }

    public static CharacterAiBranch GetBranchForActionSet(AIActionSet actionSet)
    {
        return actionSet?.Branch ?? CharacterAiBranch.None;
    }

    public static string GetActionLabel(AIActionSet actionSet)
    {
        if (actionSet == null)
        {
            return "None";
        }

        return actionSet.GetDisplayLabel();
    }

    private static bool TryPrepare(
        CharacterActor actor,
        out CharacterBlackboard blackboard,
        out string error,
        bool requireCanRunAi = true)
    {
        blackboard = actor != null ? actor.Blackboard : null;
        error = string.Empty;
        if (actor == null)
        {
            error = "Actor is missing.";
            return false;
        }

        if (blackboard == null)
        {
            error = "CharacterBlackboard is missing.";
            Debug.LogError($"{actor.name}: {error}", actor);
            return false;
        }

        if (requireCanRunAi && !actor.CanRunAi)
        {
            error = $"AI cannot run in state {actor.CurrentLifecycleState}.";
            return false;
        }

        return true;
    }

    private static CharacterAiDecisionTickResult Result(
        bool handled,
        CharacterAiBranch branch,
        string task,
        string status,
        CharacterBlackboard blackboard)
    {
        blackboard?.RecordBtStatus(branch, task, status);
        return new CharacterAiDecisionTickResult(handled, branch, task, status);
    }
}
