using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
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
    CharacterAiDecisionTickResult RunRootDecision(CharacterActor actor);
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
    private static readonly ProfilerMarker EmergencyDecisionMarker =
        new ProfilerMarker("CharacterAi.EmergencyDecision");
    private static readonly ProfilerMarker EmergencyPrepareMarker =
        new ProfilerMarker("CharacterAi.EmergencyDecision.Prepare");
    private static readonly ProfilerMarker EmergencyReliefMarker =
        new ProfilerMarker("CharacterAi.EmergencyDecision.Relief");
    private static readonly ProfilerMarker EmergencyContextMarker =
        new ProfilerMarker("CharacterAi.EmergencyDecision.Context");
    private static readonly ProfilerMarker EmergencySelectionMarker =
        new ProfilerMarker("CharacterAi.EmergencyDecision.Selection");
    private static readonly ProfilerMarker RoutineDecisionMarker =
        new ProfilerMarker("CharacterAi.RoutineDecision");
    private static readonly ProfilerMarker DomainSelectionMarker =
        new ProfilerMarker("CharacterAi.DomainSelection");
    private const float SecondaryEmergencyNeedThreshold = 0.2f;
    private readonly ICharacterDeprivationRuntime deprivationRuntime;
    private readonly List<CharacterAiJobGiver> jobGiverBuffer = new List<CharacterAiJobGiver>(10);
    private readonly List<string> rejectedCandidateBuffer = new List<string>(10);
    private readonly Dictionary<CharacterAiBranch, CharacterAiDecisionContext> decisionContextBuffer =
        new Dictionary<CharacterAiBranch, CharacterAiDecisionContext>();
    private readonly float[] jobGiverDomainScoreBuffer = new float[16];
    private readonly float[] jobGiverRankBuffer = new float[16];
    private readonly string[] jobGiverReasonBuffer = new string[16];
    private CharacterActor decisionContextActor;
    private CharacterAiDecisionContext decisionBaseContext;

    private readonly struct RoutinePriorityScores
    {
        public RoutinePriorityScores(
            float survival,
            float duty,
            float leisure,
            float idle)
        {
            Survival = survival;
            Duty = duty;
            Leisure = leisure;
            Idle = idle;
            Enabled = true;
        }

        public bool Enabled { get; }
        public float Survival { get; }
        public float Duty { get; }
        public float Leisure { get; }
        public float Idle { get; }
    }

    public CharacterAiDecisionPipeline(
        ICharacterDeprivationRuntime deprivationRuntime = null)
    {
        this.deprivationRuntime = deprivationRuntime;
    }

    public CharacterAiDecisionTickResult RunRootDecision(CharacterActor actor)
    {
        decisionContextBuffer.Clear();
        decisionContextActor = null;
        CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
        if (HasCriticalState(actor))
        {
            return RunCritical(actor, blackboard);
        }

        if (HasDeprivationBreakdown(actor))
        {
            CharacterAiDecisionTickResult deprivation = RunDeprivationBreakdown(actor);
            if (deprivation.Handled)
            {
                return deprivation;
            }
        }

        if (HasLockedAction(actor))
        {
            CharacterAiDecisionTickResult locked = RunLockedAction(actor);
            if (locked.Handled)
            {
                return locked;
            }
        }

        if (CanInterruptCurrentAction(actor))
        {
            CharacterAiDecisionTickResult interrupted = StopCurrentActionForReplan(actor);
            if (interrupted.Handled)
            {
                return interrupted;
            }
        }

        if (HasMacroGoal(actor))
        {
            CharacterAiDecisionTickResult macro = RunMacroGoalDecision(actor);
            if (macro.Handled)
            {
                return macro;
            }
        }

        CharacterAiDecisionTickResult emergency = RunEmergencyDecision(actor);
        if (emergency.Handled)
        {
            return emergency;
        }

        CharacterAiDecisionTickResult routine = RunRoutineUtilityDecision(actor);
        if (routine.Handled)
        {
            return routine;
        }

        return RunIdleBehavior(actor, blackboard);
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
        if (actor.Brain?.IsExternallyDrivenActionActive == true)
        {
            actor.Brain.UpdateExternallyDrivenAction(
                "결핍 붕괴",
                status,
                "붕괴 행동이 끝날 때까지 유지");
        }
        else
        {
            actor.Brain?.BeginExternallyDrivenAction(
                "결핍 붕괴",
                status,
                "붕괴 행동이 끝날 때까지 유지");
        }
        return Result(true, CharacterAiBranch.DeprivationBreakdown, "Run Deprivation Breakdown", status, blackboard);
    }

    public bool HasLockedAction(CharacterActor actor)
    {
        return actor != null
            && actor.Brain != null
            && actor.Brain.CanContinueCurrentAction(out _)
            && !actor.Brain.ShouldStopCurrentActionForReplan(out _)
            && !ShouldInterruptForSafeEmergencyRelief(actor, out _);
    }

    public bool CanInterruptCurrentAction(CharacterActor actor)
    {
        return actor != null
            && actor.Brain != null
            && (actor.Brain.ShouldStopCurrentActionForReplan(out _)
                || ShouldInterruptForSafeEmergencyRelief(actor, out _));
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

        CharacterAiInterruptReason interruptReason =
            CharacterAiInterruptReason.CurrentActionStopped;
        if (!actor.Brain.ShouldStopCurrentActionForReplan(out string stopReason))
        {
            if (!ShouldInterruptForSafeEmergencyRelief(actor, out stopReason))
            {
                return Result(false, CharacterAiBranch.InterruptCheck, "Interrupt Check", "Current action does not need replan.", blackboard);
            }

            interruptReason = CharacterAiInterruptReason.SurvivalEmergency;
        }

        AIAction runningAction = actor.Brain.bestAction;
        string actionLabel = GetActionLabel(runningAction?.actionset);
        actor.Brain.StopCurrentActionForReplan(stopReason);
        blackboard.ForceClearCommitment(interruptReason, stopReason);
        blackboard.SetIntent(
            CharacterAiBranch.InterruptCheck,
            actionLabel,
            "Interrupt Check",
            stopReason);
        return Result(true, CharacterAiBranch.InterruptCheck, "Interrupt Check", stopReason, blackboard);
    }

    private bool ShouldInterruptForSafeEmergencyRelief(
        CharacterActor actor,
        out string reason)
    {
        reason = string.Empty;
        return actor != null
            && actor.Brain != null
            && deprivationRuntime?.NeedsSafeEmergencyRelief(actor, out reason) == true
            && actor.Brain.CanInterruptCurrentActionForSurvivalEmergency(out _);
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
            return Result(
                false,
                branch,
                taskName,
                actor.ShouldCollectDetailedAiDiagnostics
                    ? jobCandidate.DebugSummary
                    : "후보 없음",
                blackboard);
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
        string status = actor.ShouldCollectDetailedAiDiagnostics
            ? $"{destinationLabel} | {jobCandidate.DebugSummary}"
            : actionLabel;
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
        using (EmergencyDecisionMarker.Auto())
        {
        CharacterBlackboard blackboard;
        string error;
        using (EmergencyPrepareMarker.Auto())
        {
            if (!TryPrepare(actor, out blackboard, out error))
            {
                return Result(false, CharacterAiBranch.Emergency, "Run Emergency Decision", error, blackboard);
            }
        }

        using (EmergencyReliefMarker.Auto())
        {
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
        }

        CharacterAiDecisionContext context;
        using (EmergencyContextMarker.Auto())
        {
            context = GetDecisionContext(
                actor,
                CharacterAiBranch.Emergency);
            if (actor.ShouldCollectDetailedAiDiagnostics)
            {
                blackboard.RecordDecisionContext(context);
            }
        }

        if (context.EmergencyScore < 0.58f)
        {
            return Result(
                false,
                CharacterAiBranch.Emergency,
                "Run Emergency Decision",
                actor.ShouldCollectDetailedAiDiagnostics
                    ? $"긴급도 낮음 {context.EmergencyScore * 100f:0}%"
                    : "긴급 대응 불필요",
                blackboard);
        }

        using (EmergencySelectionMarker.Auto())
        {
            IReadOnlyList<CharacterAiJobGiver> emergencyGivers =
                BuildEmergencyJobGivers(actor, context);
            return TrySelectAndRunBestJobGiver(
                actor,
                blackboard,
                CharacterAiBranch.Emergency,
                "Run Emergency Decision",
                emergencyGivers,
                default,
                out _);
        }
        }
    }

    public CharacterAiDecisionTickResult RunRoutineUtilityDecision(CharacterActor actor)
    {
        using (RoutineDecisionMarker.Auto())
        {
        if (!TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return Result(false, CharacterAiBranch.RoutineUtility, "Run Routine Utility", error, blackboard);
        }

        CharacterAiDecisionContext context = GetDecisionContext(
            actor,
            CharacterAiBranch.RoutineUtility);
        bool captureDetails = actor.ShouldCollectDetailedAiDiagnostics;
        if (captureDetails)
        {
            blackboard.RecordDecisionContext(context);
        }

        CharacterAiDecisionContext survivalContext = GetDecisionContext(
            actor,
            CharacterAiBranch.SurvivalNeeds);
        CharacterAiDecisionContext dutyContext = GetDecisionContext(
            actor,
            CharacterAiBranch.DutyWork);
        CharacterAiDecisionContext leisureContext = GetDecisionContext(
            actor,
            CharacterAiBranch.LeisureVisit);
        CharacterAiDecisionContext idleContext = GetDecisionContext(
            actor,
            CharacterAiBranch.Idle);
        float survivalPriority = CharacterAiRoutinePriority.GetPriority(
            actor,
            CharacterAiBranch.SurvivalNeeds,
            in survivalContext,
            out string survivalReason);
        float dutyPriority = CharacterAiRoutinePriority.GetPriority(
            actor,
            CharacterAiBranch.DutyWork,
            in dutyContext,
            out string dutyReason);
        float leisurePriority = CharacterAiRoutinePriority.GetPriority(
            actor,
            CharacterAiBranch.LeisureVisit,
            in leisureContext,
            out string leisureReason);
        float idlePriority = CharacterAiRoutinePriority.GetPriority(
            actor,
            CharacterAiBranch.Idle,
            in idleContext,
            out string idleReason);
        RoutinePriorityScores groupPriorities = new RoutinePriorityScores(
            survivalPriority,
            dutyPriority,
            leisurePriority,
            idlePriority);
        if (captureDetails)
        {
            blackboard.RecordRoutineGroupPriority(CharacterAiBranch.SurvivalNeeds, survivalPriority, survivalReason);
            blackboard.RecordRoutineGroupPriority(CharacterAiBranch.DutyWork, dutyPriority, dutyReason);
            blackboard.RecordRoutineGroupPriority(CharacterAiBranch.LeisureVisit, leisurePriority, leisureReason);
            blackboard.RecordRoutineGroupPriority(CharacterAiBranch.Idle, idlePriority, idleReason);
        }

        CharacterAiDecisionTickResult result = TrySelectAndRunBestJobGiver(
            actor,
            blackboard,
            CharacterAiBranch.RoutineUtility,
            "Run Routine Utility",
            BuildRoutineJobGivers(actor, in context),
            groupPriorities,
            out string failure);
        if (result.Handled)
        {
            return result;
        }

        return Result(false, CharacterAiBranch.RoutineUtility, "Run Routine Utility", failure, blackboard);
        }
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
        IReadOnlyList<CharacterAiJobGiver> jobGivers,
        RoutinePriorityScores routinePriorities,
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
        bool captureDetails = actor.ShouldCollectDetailedAiDiagnostics;
        FacilityRole availableFacilityRoles =
            CharacterAiJobGiver.ResolveAvailableFacilityRoles(actor);
        rejectedCandidateBuffer.Clear();
        int jobGiverCount = jobGivers != null ? jobGivers.Count : 0;
        if (!captureDetails
            && jobGiverCount > 0
            && jobGiverCount <= jobGiverDomainScoreBuffer.Length)
        {
            return TrySelectAndRunHierarchicalJobGiver(
                actor,
                blackboard,
                branchOverride,
                taskName,
                jobGivers,
                jobGiverCount,
                routinePriorities,
                availableFacilityRoles,
                out failureSummary);
        }

        for (int i = 0; i < jobGiverCount; i++)
        {
            CharacterAiJobGiver jobGiver = jobGivers[i];
            if (jobGiver == null)
            {
                continue;
            }

            CharacterAiDecisionContext context = GetDecisionContext(
                actor,
                jobGiver.Branch);

            float domainScore = jobGiver.EvaluateDomain(
                actor,
                in context,
                availableFacilityRoles,
                captureDetails,
                out string domainReason);
            if (!jobGiver.TryEvaluate(
                    actor,
                    in context,
                    domainScore,
                    domainReason,
                    out CharacterAiJobCandidate candidate))
            {
                if (candidate.ActionCandidate.Failure.Kind
                        == AIActionFailureKind.PathSearchDeferred
                    && actor.Brain.IsActionScoringPending)
                {
                    failureSummary = "행동 후보를 나누어 평가하는 중";
                    return Result(
                        true,
                        branchOverride,
                        taskName,
                        failureSummary,
                        blackboard);
                }

                if (captureDetails)
                {
                    string debugSummary = candidate.DebugSummary;
                    rejectedCandidateBuffer.Add(debugSummary);
                    blackboard.RecordJobGiverUtility(jobGiver.Branch, 0f, debugSummary);
                }

                continue;
            }

            float multiplier = GetGroupPriorityMultiplier(jobGiver.Branch, routinePriorities);
            float adjusted = Mathf.Clamp01(candidate.Utility * multiplier);
            adjusted = CharacterMoodImpulseUtility.ApplyFinalAutonomyBias(
                actor,
                candidate.Branch,
                adjusted,
                candidate.ActionCandidate.Action);
            if (captureDetails)
            {
                blackboard.RecordJobGiverUtility(
                    jobGiver.Branch,
                    adjusted,
                    $"{candidate.DebugSummary} group={multiplier:0.##}");
            }

            if (!hasCandidate || adjusted > bestAdjustedUtility)
            {
                bestCandidate = candidate;
                bestAdjustedUtility = adjusted;
                hasCandidate = true;
            }
        }

        if (!hasCandidate)
        {
            failureSummary = captureDetails && rejectedCandidateBuffer.Count > 0
                ? string.Join(" / ", rejectedCandidateBuffer.Take(3))
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
        string status = captureDetails
            ? $"{actionLabel} · {bestCandidate.DebugSummary}"
            : actionLabel;
        actor.AiMemory?.RecordDecision(
            branchOverride,
            CharacterAiUtilityText.GetIntention(bestCandidate.Branch),
            status,
            0.1f);
        return Result(runResult.Handled, branchOverride, taskName, status, blackboard);
    }

    private CharacterAiDecisionTickResult TrySelectAndRunHierarchicalJobGiver(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterAiBranch branchOverride,
        string taskName,
        IReadOnlyList<CharacterAiJobGiver> jobGivers,
        int jobGiverCount,
        RoutinePriorityScores routinePriorities,
        FacilityRole availableFacilityRoles,
        out string failureSummary)
    {
        using (DomainSelectionMarker.Auto())
        {
        ICharacterAiPerformanceRecorder recorder =
            actor?.Brain?.PerformanceRecorder;
        bool collectDomainPerformance =
            recorder?.DetailedCollectionEnabled == true;
        long domainStarted = collectDomainPerformance
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        long domainAllocatedAtStart = collectDomainPerformance
            ? GC.GetAllocatedBytesForCurrentThread()
            : 0L;
        int remainingMask = 0;
        for (int index = 0; index < jobGiverCount; index++)
        {
            CharacterAiJobGiver jobGiver = jobGivers[index];
            if (jobGiver == null)
            {
                jobGiverDomainScoreBuffer[index] = 0f;
                jobGiverRankBuffer[index] = 0f;
                jobGiverReasonBuffer[index] = string.Empty;
                continue;
            }

            CharacterAiDecisionContext context = GetDecisionContext(
                actor,
                jobGiver.Branch);
            float domainScore = jobGiver.EvaluateDomain(
                actor,
                in context,
                availableFacilityRoles,
                false,
                out string domainReason);
            float groupMultiplier = GetGroupPriorityMultiplier(
                jobGiver.Branch,
                routinePriorities);
            jobGiverDomainScoreBuffer[index] = domainScore;
            jobGiverRankBuffer[index] = domainScore * groupMultiplier;
            jobGiverReasonBuffer[index] = domainReason;
            if (domainScore > 0f)
            {
                remainingMask |= 1 << index;
            }
        }
        if (domainStarted != 0L)
        {
            recorder.Record(
                AiPerformanceCategory.DomainSelection,
                (System.Diagnostics.Stopwatch.GetTimestamp() - domainStarted)
                * 1000.0
                / System.Diagnostics.Stopwatch.Frequency,
                Math.Max(
                    0L,
                    GC.GetAllocatedBytesForCurrentThread()
                        - domainAllocatedAtStart));
        }

        AIActionFailure lastFailure = AIActionFailure.Create(
            AIActionFailureKind.NoDestination);
        while (remainingMask != 0)
        {
            int bestIndex = -1;
            float bestRank = float.MinValue;
            for (int index = 0; index < jobGiverCount; index++)
            {
                if ((remainingMask & (1 << index)) == 0
                    || jobGiverRankBuffer[index] <= bestRank)
                {
                    continue;
                }

                bestIndex = index;
                bestRank = jobGiverRankBuffer[index];
            }

            if (bestIndex < 0)
            {
                break;
            }

            remainingMask &= ~(1 << bestIndex);
            CharacterAiJobGiver jobGiver = jobGivers[bestIndex];
            CharacterAiDecisionContext context = GetDecisionContext(
                actor,
                jobGiver.Branch);
            if (!jobGiver.TryEvaluate(
                actor,
                in context,
                jobGiverDomainScoreBuffer[bestIndex],
                jobGiverReasonBuffer[bestIndex],
                out CharacterAiJobCandidate candidate))
            {
                lastFailure = candidate.ActionCandidate.Failure;
                if (lastFailure.Kind == AIActionFailureKind.PathSearchDeferred
                    && actor.Brain.IsActionScoringPending)
                {
                    failureSummary = "행동 후보를 나누어 평가하는 중";
                    return Result(
                        true,
                        branchOverride,
                        taskName,
                        failureSummary,
                        blackboard);
                }

                continue;
            }

            float groupMultiplier = GetGroupPriorityMultiplier(
                jobGiver.Branch,
                routinePriorities);
            float adjusted = Mathf.Clamp01(candidate.Utility * groupMultiplier);
            adjusted = CharacterMoodImpulseUtility.ApplyFinalAutonomyBias(
                actor,
                candidate.Branch,
                adjusted,
                candidate.ActionCandidate.Action);
            if (adjusted <= 0f)
            {
                continue;
            }

            blackboard.RecordSelectedJobGiverUtility(candidate);
            if (!actor.Brain.TryCommitActionCandidate(
                candidate.ActionCandidate,
                out AIActionFailure failure))
            {
                blackboard.ReportActionFailure(null, failure);
                lastFailure = failure;
                continue;
            }

            CharacterAiDecisionTickResult runResult = RunSelectedAction(
                actor,
                taskName,
                branchOverride);
            actor.AiMemory?.RecordDecision(
                branchOverride,
                CharacterAiUtilityText.GetIntention(candidate.Branch),
                GetActionLabel(actor.Brain.bestAction?.actionset),
                0.1f);
            failureSummary = string.Empty;
            return Result(
                runResult.Handled,
                branchOverride,
                taskName,
                GetActionLabel(actor.Brain.bestAction?.actionset),
                blackboard);
        }

        failureSummary = lastFailure.HasFailure
            ? lastFailure.ToString()
            : "No valid utility candidates.";
        return Result(
            false,
            branchOverride,
            taskName,
            failureSummary,
            blackboard);
        }
    }

    private CharacterAiDecisionContext GetDecisionContext(
        CharacterActor actor,
        CharacterAiBranch branch)
    {
        if (decisionContextBuffer.TryGetValue(
                branch,
                out CharacterAiDecisionContext context)
            && context.Actor == actor)
        {
            return context;
        }

        ICharacterAiPerformanceRecorder recorder =
            actor?.Brain?.PerformanceRecorder;
        bool collectContextPerformance =
            recorder?.DetailedCollectionEnabled == true;
        long started = collectContextPerformance
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        long allocatedAtStart = collectContextPerformance
            ? GC.GetAllocatedBytesForCurrentThread()
            : 0L;
        if (decisionContextActor == actor)
        {
            context = decisionBaseContext.WithBranch(branch);
        }
        else
        {
            context = CharacterAiDecisionContext.Capture(actor, branch);
            decisionContextActor = actor;
            decisionBaseContext = context;
        }
        if (started != 0L)
        {
            recorder.Record(
                AiPerformanceCategory.DecisionContext,
                (System.Diagnostics.Stopwatch.GetTimestamp() - started)
                * 1000.0
                / System.Diagnostics.Stopwatch.Frequency,
                Math.Max(
                    0L,
                    GC.GetAllocatedBytesForCurrentThread()
                        - allocatedAtStart));
        }

        decisionContextBuffer[branch] = context;
        return context;
    }

    private IReadOnlyList<CharacterAiJobGiver> BuildEmergencyJobGivers(
        CharacterActor actor,
        CharacterAiDecisionContext context)
    {
        ICharacterAiJobGiverCatalog catalog = RequireJobGiverCatalog(actor);
        jobGiverBuffer.Clear();
        if (context.HungerUrgency >= SecondaryEmergencyNeedThreshold)
        {
            AddUniqueJobGiver(catalog.GetFood);
        }

        if (context.ExcretionUrgency >= SecondaryEmergencyNeedThreshold)
        {
            AddUniqueJobGiver(catalog.Toilet);
        }

        if (context.HygieneUrgency >= SecondaryEmergencyNeedThreshold)
        {
            AddUniqueJobGiver(catalog.Hygiene);
        }

        if (context.SleepUrgency >= SecondaryEmergencyNeedThreshold
            || context.InjuryUrgency > 0.35f
            || context.HealthUrgency > 0.45f)
        {
            AddUniqueJobGiver(catalog.Rest);
        }

        if (context.IsWorker
            && (HasExplicitPriorityWork(actor)
                || context.FoodStockPressure > 0.65f
                || context.WaterStockPressure > 0.65f))
        {
            AddUniqueJobGiver(catalog.Work);
        }

        if (!context.IsWorker)
        {
            AddUniqueJobGiver(catalog.ExitDungeon);
        }

        AddUniqueJobGiver(catalog.Wait);
        return jobGiverBuffer;
    }

    private static bool HasExplicitPriorityWork(CharacterActor actor)
    {
        return actor != null
            && actor.TryGetAbility(out AbilityWork work)
            && (work.PriorityWorkTarget != null || work.HasPrioritySuppressTarget);
    }

    private IReadOnlyList<CharacterAiJobGiver> BuildRoutineJobGivers(
        CharacterActor actor,
        in CharacterAiDecisionContext context)
    {
        ICharacterAiJobGiverCatalog catalog = RequireJobGiverCatalog(actor);
        jobGiverBuffer.Clear();
        AddUniqueJobGiver(catalog.GetFood);
        AddUniqueJobGiver(catalog.Toilet);
        AddUniqueJobGiver(catalog.Hygiene);
        AddUniqueJobGiver(catalog.Rest);

        if (context.IsWorker)
        {
            AddUniqueJobGiver(catalog.Work);
            AddUniqueJobGiver(catalog.Wait);
        }
        else
        {
            AddUniqueJobGiver(catalog.Shopping);
            AddUniqueJobGiver(catalog.LookAround);
            AddUniqueJobGiver(catalog.ExitDungeon);
        }

        return jobGiverBuffer;
    }

    private void AddUniqueJobGiver(CharacterAiJobGiver jobGiver)
    {
        if (jobGiver != null && !jobGiverBuffer.Contains(jobGiver))
        {
            jobGiverBuffer.Add(jobGiver);
        }
    }

    private static float GetGroupPriorityMultiplier(
        CharacterAiBranch jobBranch,
        RoutinePriorityScores priorities)
    {
        if (!priorities.Enabled)
        {
            return 1f;
        }

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
        float priority = group switch
        {
            CharacterAiBranch.SurvivalNeeds => priorities.Survival,
            CharacterAiBranch.DutyWork => priorities.Duty,
            CharacterAiBranch.LeisureVisit => priorities.Leisure,
            _ => priorities.Idle
        };
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
