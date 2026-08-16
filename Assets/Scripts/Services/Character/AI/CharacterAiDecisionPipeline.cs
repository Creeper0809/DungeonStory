using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;

public sealed class CharacterAiDecisionPipeline : ICharacterAiDecisionPipeline
{
    private static readonly CharacterBreakdownKind[] BreakdownKinds =
        (CharacterBreakdownKind[])Enum.GetValues(
            typeof(CharacterBreakdownKind));
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
    private readonly ICharacterDeprivationQuery deprivationQuery;
    private readonly ICharacterDeprivationCommand deprivationCommands;
    private readonly CharacterAiMacroDecisionRunner macroDecisions;
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
        ICharacterDeprivationQuery deprivationQuery,
        ICharacterDeprivationCommand deprivationCommands,
        ICharacterWorldQuery characterWorld = null,
        CharacterIdentityEventPublisher identityEvents = null,
        IGameCalendar calendar = null)
    {
        this.deprivationQuery = deprivationQuery
            ?? throw new ArgumentNullException(nameof(deprivationQuery));
        this.deprivationCommands = deprivationCommands
            ?? throw new ArgumentNullException(nameof(deprivationCommands));
        macroDecisions = new CharacterAiMacroDecisionRunner(
            RunSelectedAction,
            characterWorld,
            identityEvents,
            calendar);
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

        if (actor?.Brain != null
            && actor.Brain.TryConsumePreferredActionHardFailureDecision(
                out AIActionFailure preferredFailure,
                out CharacterAiPreferredActionFailureSource failureSource))
        {
            // An explicit preferred action failed terminally in this decision.
            // Preserve that typed result as the decision outcome and let the
            // scheduler retry at its bounded failure cadence. Committing an
            // unrelated Wait/Haul here overwrites the root cause and creates a
            // false successful epoch between the command and its next plan.
            return CharacterAiDecisionRules.Result(
                false,
                CharacterAiBranch.RoutineUtility,
                "Preferred Action Hard Failure",
                $"{failureSource}:{preferredFailure.Kind}:"
                    + preferredFailure.ToString(),
                blackboard);
        }

        // Destination commit can legitimately yield after the candidate has
        // been scored (for example while an incremental exact path consumes
        // its next broker slice). That state is scheduler-owned backpressure,
        // not an Idle decision. Starting ambient idle here used to clear the
        // pending path state, report the root as handled, and strand the
        // preferred survival action until the ordinary cadence tick.
        if (actor?.Brain != null
            && (actor.Brain.IsActionScoringPending
                || actor.Brain.IsPathSearchDeferred
                || actor.Brain.IsPreferredActionDeferred))
        {
            return CharacterAiDecisionRules.Result(
                true,
                CharacterAiBranch.RoutineUtility,
                "Deferred Routine Retry",
                actor.Brain.LastActionFailure.HasFailure
                    ? actor.Brain.LastActionFailure.ToString()
                    : "Routine candidate evaluation is deferred.",
                blackboard);
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
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.Critical, "HasCriticalState", "Actor is missing.", blackboard);
        }

        if (actor.Brain != null && actor.Brain.IsManualCommandActive)
        {
            return CharacterAiDecisionRules.Result(
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

        return CharacterAiDecisionRules.Result(true, CharacterAiBranch.Critical, "HasCriticalState", actor.CurrentLifecycleState.ToString(), blackboard);
    }

    public bool HasMacroGoal(CharacterActor actor)
    {
        return actor != null && actor.Blackboard != null && actor.Blackboard.HasActiveMacroGoal();
    }

    public bool HasDeprivationBreakdown(CharacterActor actor)
    {
        return deprivationQuery.HasActiveBreakdown(actor);
    }

    public CharacterAiDecisionTickResult RunDeprivationBreakdown(CharacterActor actor)
    {
        CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
        CharacterBreakdownKind breakdownKind = ResolveActiveBreakdownKind(actor);
        if (!deprivationCommands.TryRunActiveBreakdown(actor, out string status))
        {
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.DeprivationBreakdown, "Run Deprivation Breakdown", "활성 붕괴 없음", blackboard);
        }

        if (breakdownKind == CharacterBreakdownKind.None)
        {
            throw new InvalidOperationException(
                "A deprivation breakdown ran without a typed active kind.");
        }

        blackboard?.RecordHandledDeprivationBreakdown(breakdownKind);
        blackboard?.SetIntent(
            CharacterAiBranch.DeprivationBreakdown,
            status,
            "Run Deprivation Breakdown",
            status);
        return CharacterAiDecisionRules.Result(true, CharacterAiBranch.DeprivationBreakdown, "Run Deprivation Breakdown", status, blackboard);
    }

    private CharacterBreakdownKind ResolveActiveBreakdownKind(
        CharacterActor actor)
    {
        for (int index = 0; index < BreakdownKinds.Length; index++)
        {
            CharacterBreakdownKind kind = BreakdownKinds[index];
            if (kind != CharacterBreakdownKind.None
                && deprivationQuery.HasBreakdownKind(actor, kind))
            {
                return kind;
            }
        }

        return CharacterBreakdownKind.None;
    }

    public bool HasLockedAction(CharacterActor actor)
    {
        return actor != null
            && actor.Brain != null
            && actor.Brain.CanContinueCurrentAction(out _)
            && !actor.Brain.ShouldStopCurrentActionForReplan(out _)
            && !ShouldInterruptForSurvivalEmergency(actor, out _);
    }

    public bool CanInterruptCurrentAction(CharacterActor actor)
    {
        return actor != null
            && actor.Brain != null
            && (actor.Brain.ShouldStopCurrentActionForReplan(out _)
                || ShouldInterruptForSurvivalEmergency(actor, out _));
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
        if (!CharacterAiDecisionRules.TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.LockedAction, "Locked Action", error, blackboard);
        }

        if (actor.Brain == null)
        {
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.LockedAction, "Locked Action", "AIBrain is missing.", blackboard);
        }

        if (actor.Brain.ShouldStopCurrentActionForReplan(out string stopReason))
        {
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.LockedAction, "Locked Action", stopReason, blackboard);
        }

        if (!actor.Brain.CanContinueCurrentAction(out string status))
        {
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.LockedAction, "Locked Action", status, blackboard);
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
        return CharacterAiDecisionRules.Result(true, CharacterAiBranch.LockedAction, "Locked Action", status, blackboard);
    }

    public CharacterAiDecisionTickResult ContinueCurrentAction(CharacterActor actor)
    {
        if (!CharacterAiDecisionRules.TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.ContinueCurrent, "Continue Current Action", error, blackboard);
        }

        if (actor.Brain == null)
        {
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.ContinueCurrent, "Continue Current Action", "AIBrain is missing.", blackboard);
        }

        if (!actor.Brain.CanContinueCurrentAction(out string status))
        {
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.ContinueCurrent, "Continue Current Action", status, blackboard);
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
        return CharacterAiDecisionRules.Result(true, CharacterAiBranch.ContinueCurrent, "Continue Current Action", status, blackboard);
    }

    public CharacterAiDecisionTickResult StopCurrentActionForReplan(CharacterActor actor)
    {
        if (!CharacterAiDecisionRules.TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.InterruptCheck, "Interrupt Check", error, blackboard);
        }

        if (actor.Brain == null)
        {
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.InterruptCheck, "Interrupt Check", "AIBrain is missing.", blackboard);
        }

        CharacterAiInterruptReason interruptReason =
            CharacterAiInterruptReason.CurrentActionStopped;
        if (!actor.Brain.ShouldStopCurrentActionForReplan(out string stopReason))
        {
            if (!ShouldInterruptForSurvivalEmergency(actor, out stopReason))
            {
                return CharacterAiDecisionRules.Result(false, CharacterAiBranch.InterruptCheck, "Interrupt Check", "Current action does not need replan.", blackboard);
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
        return CharacterAiDecisionRules.Result(true, CharacterAiBranch.InterruptCheck, "Interrupt Check", stopReason, blackboard);
    }

    private bool ShouldInterruptForSurvivalEmergency(
        CharacterActor actor,
        out string reason)
    {
        reason = string.Empty;
        if (actor == null
            || actor.Brain == null
            || !actor.Brain.CanInterruptCurrentActionForSurvivalEmergency(out _))
        {
            return false;
        }

        if (deprivationQuery.NeedsSafeEmergencyRelief(actor, out reason))
        {
            return true;
        }

        if (CharacterNeedAiThresholds.IsEmergencyOrImminentPhysicalHarm(
                actor,
                CharacterCondition.HUNGER)
            && (deprivationQuery.NeedsPrimitiveMeal(actor, out _)
                || FacilityCandidateScorer.HasUsableCandidate(actor, FacilityRole.Meal)))
        {
            reason = "Emergency hunger relief is available.";
            return true;
        }

        if (CharacterNeedAiThresholds.IsEmergency(actor, CharacterCondition.SLEEP)
            && (deprivationQuery.NeedsPrimitiveRest(actor, out _)
                || FacilityCandidateScorer.HasUsableCandidate(actor, FacilityRole.Rest)))
        {
            reason = "Emergency sleep relief is available.";
            return true;
        }

        if (CharacterNeedAiThresholds.IsEmergency(actor, CharacterCondition.EXCRETION)
            && (deprivationQuery.NeedsPrimitiveRelief(actor, out _)
                || FacilityCandidateScorer.HasUsableCandidate(actor, FacilityRole.Toilet)))
        {
            reason = "Emergency excretion relief is available.";
            return true;
        }

        if (CharacterNeedAiThresholds.IsEmergency(actor, CharacterCondition.HYGIENE)
            && (deprivationQuery.NeedsPrimitiveWash(actor, out _)
                || FacilityCandidateScorer.HasUsableCandidate(actor, FacilityRole.Hygiene)))
        {
            reason = "Emergency hygiene relief is available.";
            return true;
        }

        return false;
    }

    public CharacterAiDecisionTickResult SelectJobGiverAction(
        CharacterActor actor,
        CharacterAiJobGiver jobGiver,
        string taskName)
    {
        CharacterAiBranch branch = jobGiver != null
            ? jobGiver.Branch
            : CharacterAiBranch.None;
        if (!CharacterAiDecisionRules.TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return CharacterAiDecisionRules.Result(false, branch, taskName, error, blackboard);
        }

        if (actor.Brain == null)
        {
            return CharacterAiDecisionRules.Result(false, branch, taskName, "AIBrain is missing.", blackboard);
        }

        if (jobGiver == null)
        {
            return CharacterAiDecisionRules.Result(false, branch, taskName, "JobGiver is missing.", blackboard);
        }

        if (!blackboard.TryGetCachedJobGiverCandidate(branch, out CharacterAiJobCandidate jobCandidate)
            && !jobGiver.TryEvaluate(actor, out jobCandidate))
        {
            if (jobGiver.MatchesPreferredAction(actor.Brain)
                && actor.Brain.TryRetainPreferredBranchDeferred(
                    branch,
                    jobCandidate.ActionCandidate.Failure))
            {
                actor.Brain.PreservePreferredDeferredDecisionOwnership();
            }
            else if (jobGiver.MatchesPreferredAction(actor.Brain))
            {
                actor.Brain.RetirePreferredBranchAfterHardFailure(
                    branch,
                    jobCandidate.ActionCandidate.Failure,
                    CharacterAiPreferredActionFailureSource
                        .JobGiverActionEvaluation);
            }
            return CharacterAiDecisionRules.Result(
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
            if (jobGiver.MatchesPreferredAction(actor.Brain))
            {
                actor.Brain.RetirePreferredBranchAfterHardFailure(
                    branch,
                    failure,
                    CharacterAiPreferredActionFailureSource
                        .JobGiverCandidateCommit);
            }
            return CharacterAiDecisionRules.Result(false, branch, taskName, failure.ToString(), blackboard);
        }

        AIAction selectedAction = actor.Brain.bestAction;
        string actionLabel = GetActionLabel(selectedAction?.actionset);
        string destinationLabel = CharacterAiDecisionRules.GetBuildingLabel(selectedAction?.destination);
        string status = actor.ShouldCollectDetailedAiDiagnostics
            ? $"{destinationLabel} | {jobCandidate.DebugSummary}"
            : actionLabel;
        blackboard.SetIntent(branch, actionLabel, taskName, status);
        return CharacterAiDecisionRules.Result(true, branch, taskName, status, blackboard);
    }

    public CharacterAiDecisionTickResult RunSelectedAction(
        CharacterActor actor,
        string taskName,
        CharacterAiBranch branchOverride = CharacterAiBranch.None)
    {
        if (!CharacterAiDecisionRules.TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.None, taskName, error, blackboard);
        }

        AIAction selectedAction = actor.Brain != null ? actor.Brain.bestAction : null;
        CharacterAiBranch branch = branchOverride != CharacterAiBranch.None
            ? branchOverride
            : GetBranchForActionSet(selectedAction?.actionset);
        if (selectedAction == null || selectedAction.actionset == null)
        {
            AIActionFailure failure = AIActionFailure.Create(
                AIActionFailureKind.NoAction,
                "No selected AI action.");
            actor.Brain?.ReportRuntimeActionFailure(
                failure,
                requestImmediateReplan: true);
            return CharacterAiDecisionRules.Result(
                false,
                branch,
                taskName,
                $"{failure.Kind}: {failure}",
                blackboard);
        }

        bool executed = actor.TryExecuteSelectedAiAction(
            out AIActionFailure executionFailure);
        if (executed)
        {
            blackboard.RefreshCommitment(selectedAction);
        }
        else
        {
            actor.Brain?.ReportRuntimeActionFailure(
                executionFailure,
                requestImmediateReplan: true);
        }

        return CharacterAiDecisionRules.Result(
            executed,
            branch,
            taskName,
            executed
                ? GetActionLabel(selectedAction.actionset)
                : $"{executionFailure.Kind}: {executionFailure}",
            blackboard);
    }

    public CharacterAiDecisionTickResult RunMacroGoalDecision(CharacterActor actor) =>
        macroDecisions.RunGoal(actor);

    public CharacterAiDecisionTickResult RunIdleBehavior(CharacterActor actor, CharacterBlackboard blackboard)
    {
        if (!CharacterAiDecisionRules.TryPrepare(actor, out blackboard, out string error))
        {
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.Idle, "RunIdleBehavior", error, blackboard);
        }

        if (!actor.TryGetAbility(out AbilityMove _))
        {
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.Idle, "RunIdleBehavior", "AbilityMove is missing.", blackboard);
        }

        if (actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = false;
        }

        bool survivalNeedDue = CharacterNeedAiThresholds
            .TryGetMostUrgentSurvivalRoutineNeed(
                actor,
                out CharacterCondition survivalCondition,
                out float survivalUtility,
                out float survivalValue,
                out float survivalThreshold);
        string behaviorName;
        string failureReason;
        bool idleStarted = survivalNeedDue
            ? IdleBehaviorRunner.TryRunStatic(
                actor,
                1.0f,
                out behaviorName,
                out failureReason)
            : IdleBehaviorRunner.TryRunDefault(
                actor,
                1.0f,
                true,
                out behaviorName,
                out failureReason);
        if (idleStarted)
        {
            if (survivalNeedDue)
            {
                behaviorName = $"생존 욕구 대기({survivalCondition} {survivalValue:0.###}/{survivalThreshold:0.###}, utility={survivalUtility:0.###})";
            }
            actor.Brain?.ClearSelectedActionForIdle(behaviorName);
            blackboard.ClearCommitment(CharacterAiInterruptReason.ManualReplan, "Idle behavior selected.");
            blackboard.RecordSelectedUtilitySummary($"AmbientIdle utility=explicit behavior={behaviorName}");
            blackboard.SetIntent(CharacterAiBranch.Idle, behaviorName, "RunIdleBehavior", behaviorName);
            actor.AddActivity(CharacterActivityEvent.InternalAi(
                CharacterActivityOutcomes.Started,
                "idle-selected",
                $"AI idle: {behaviorName}"));
            return CharacterAiDecisionRules.Result(true, CharacterAiBranch.Idle, "RunIdleBehavior", behaviorName, blackboard);
        }

        actor.AddActivity(CharacterActivityEvent.InternalAi(
            CharacterActivityOutcomes.Failed,
            "idle-failed",
            $"AI idle failed: {failureReason}"));
        if (actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
        }

        return CharacterAiDecisionRules.Result(false, CharacterAiBranch.Idle, "RunIdleBehavior", failureReason, blackboard);
    }

    public CharacterAiDecisionTickResult RunEmergencyDecision(CharacterActor actor)
    {
        using (EmergencyDecisionMarker.Auto())
        {
        CharacterBlackboard blackboard;
        string error;
        using (EmergencyPrepareMarker.Auto())
        {
            if (!CharacterAiDecisionRules.TryPrepare(actor, out blackboard, out error))
            {
                return CharacterAiDecisionRules.Result(false, CharacterAiBranch.Emergency, "Run Emergency Decision", error, blackboard);
            }
        }

        using (EmergencyReliefMarker.Auto())
        {
            if (HasDeprivationBreakdown(actor))
            {
                return RunDeprivationBreakdown(actor);
            }
        }

        // Emergency needs must use the same job-giver/action candidate path as
        // routine needs. The former direct deprivation shortcut started a
        // primitive action before the authored facility action could be
        // evaluated, so a free toilet could still lose to a field latrine.
        // The emergency job-giver set already contains both facility and
        // primitive actions and applies the shared availability policy.

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
            return CharacterAiDecisionRules.Result(
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
        if (!CharacterAiDecisionRules.TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return CharacterAiDecisionRules.Result(false, CharacterAiBranch.RoutineUtility, "Run Routine Utility", error, blackboard);
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

        return CharacterAiDecisionRules.Result(false, CharacterAiBranch.RoutineUtility, "Run Routine Utility", failure, blackboard);
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
        return CharacterAiDecisionRules.Result(actor != null, branch, taskName, status, blackboard);
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
            return CharacterAiDecisionRules.Result(false, branchOverride, taskName, failureSummary, blackboard);
        }

        CharacterAiJobCandidate bestCandidate = default;
        float bestAdjustedUtility = float.MinValue;
        bool hasCandidate = false;
        bool bestCandidatePreferred = false;
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

        actor.Brain.BeginJobGiverEvaluationPass();

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
            bool jobGiverPreferred =
                jobGiver.MatchesPreferredAction(actor.Brain);

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
                if (domainScore > 0f
                    || candidate.ActionCandidate.Failure.HasFailure)
                {
                    actor.Brain.RecordJobGiverEvaluationRejection(
                        jobGiver.Branch,
                        candidate.ActionCandidate.Failure,
                        candidate.Reason);
                }
                bool preferredDeferred = jobGiverPreferred
                    && actor.Brain.TryRetainPreferredBranchDeferred(
                        jobGiver.Branch,
                        candidate.ActionCandidate.Failure);
                if ((candidate.ActionCandidate.Failure.IsDeferred
                        && actor.Brain.IsActionScoringPending)
                    || preferredDeferred)
                {
                    if (preferredDeferred)
                    {
                        actor.Brain
                            .PreservePreferredDeferredDecisionOwnership();
                    }
                    failureSummary = "행동 후보를 나누어 평가하는 중";
                    return CharacterAiDecisionRules.Result(
                        true,
                        branchOverride,
                        taskName,
                        failureSummary,
                        blackboard);
                }
                if (jobGiverPreferred)
                {
                    actor.Brain.RetirePreferredBranchAfterHardFailure(
                        jobGiver.Branch,
                        candidate.ActionCandidate.Failure,
                        CharacterAiPreferredActionFailureSource
                            .JobGiverActionEvaluation);
                    return CharacterAiDecisionRules.Result(
                        false,
                        branchOverride,
                        taskName,
                        candidate.DebugSummary,
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

            bool candidatePreferred = jobGiverPreferred;
            if (!hasCandidate
                || (candidatePreferred && !bestCandidatePreferred)
                || (candidatePreferred == bestCandidatePreferred
                    && adjusted > bestAdjustedUtility))
            {
                bestCandidate = candidate;
                bestAdjustedUtility = adjusted;
                hasCandidate = true;
                bestCandidatePreferred = candidatePreferred;
            }
        }

        if (!hasCandidate)
        {
            failureSummary = captureDetails && rejectedCandidateBuffer.Count > 0
                ? string.Join(" / ", rejectedCandidateBuffer.Take(3))
                : "No valid utility candidates.";
            return CharacterAiDecisionRules.Result(false, branchOverride, taskName, failureSummary, blackboard);
        }

        blackboard.RecordSelectedJobGiverUtility(bestCandidate);
        if (!actor.Brain.TryCommitActionCandidate(bestCandidate.ActionCandidate, out AIActionFailure failure))
        {
            blackboard.ReportActionFailure(null, failure);
            failureSummary = failure.ToString();
            if (bestCandidatePreferred
                && actor.Brain.TryRetainPreferredBranchDeferred(
                    bestCandidate.Branch,
                    failure))
            {
                actor.Brain.PreservePreferredDeferredDecisionOwnership();
                return CharacterAiDecisionRules.Result(
                    true,
                    branchOverride,
                    taskName,
                    failureSummary,
                    blackboard);
            }
            if (bestCandidatePreferred)
            {
                actor.Brain.RetirePreferredBranchAfterHardFailure(
                    bestCandidate.Branch,
                    failure,
                    CharacterAiPreferredActionFailureSource
                        .JobGiverCandidateCommit);
            }
            return CharacterAiDecisionRules.Result(false, branchOverride, taskName, failureSummary, blackboard);
        }

        CharacterAiDecisionTickResult runResult = RunSelectedAction(actor, taskName, branchOverride);
        AIAction selectedAction = actor.Brain.bestAction;
        string actionLabel = GetActionLabel(selectedAction?.actionset);
        string status = !runResult.Handled
            ? runResult.Status
            : captureDetails
                ? $"{actionLabel} · {bestCandidate.DebugSummary}"
                : actionLabel;
        actor.AiMemory?.RecordDecision(
            branchOverride,
            CharacterAiUtilityText.GetIntention(bestCandidate.Branch),
            status,
            0.1f);
        return CharacterAiDecisionRules.Result(runResult.Handled, branchOverride, taskName, status, blackboard);
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
        actor?.Brain?.BeginJobGiverEvaluationPass();
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
            bool bestPreferred = false;
            for (int index = 0; index < jobGiverCount; index++)
            {
                if ((remainingMask & (1 << index)) == 0)
                {
                    continue;
                }

                CharacterAiJobGiver rankedJobGiver = jobGivers[index];
                bool preferred = rankedJobGiver != null
                    && rankedJobGiver.MatchesPreferredAction(actor.Brain);
                if (bestIndex >= 0
                    && ((!preferred && bestPreferred)
                        || (preferred == bestPreferred
                            && jobGiverRankBuffer[index] <= bestRank)))
                {
                    continue;
                }

                bestIndex = index;
                bestRank = jobGiverRankBuffer[index];
                bestPreferred = preferred;
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
                actor.Brain.RecordJobGiverEvaluationRejection(
                    jobGiver.Branch,
                    lastFailure,
                    candidate.Reason);
                bool preferredDeferred = bestPreferred
                    && actor.Brain.TryRetainPreferredBranchDeferred(
                        jobGiver.Branch,
                        lastFailure);
                if ((lastFailure.IsDeferred
                        && actor.Brain.IsActionScoringPending)
                    || preferredDeferred)
                {
                    if (preferredDeferred)
                    {
                        actor.Brain
                            .PreservePreferredDeferredDecisionOwnership();
                    }
                    failureSummary = "행동 후보를 나누어 평가하는 중";
                    return CharacterAiDecisionRules.Result(
                        true,
                        branchOverride,
                        taskName,
                        failureSummary,
                        blackboard);
                }
                if (bestPreferred)
                {
                    actor.Brain.RetirePreferredBranchAfterHardFailure(
                        jobGiver.Branch,
                        lastFailure,
                        CharacterAiPreferredActionFailureSource
                            .JobGiverActionEvaluation);
                    failureSummary = candidate.DebugSummary;
                    return CharacterAiDecisionRules.Result(
                        false,
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
                if (bestPreferred
                    && actor.Brain.TryRetainPreferredBranchDeferred(
                        candidate.Branch,
                        failure))
                {
                    actor.Brain
                        .PreservePreferredDeferredDecisionOwnership();
                    failureSummary = failure.ToString();
                    return CharacterAiDecisionRules.Result(
                        true,
                        branchOverride,
                        taskName,
                        failureSummary,
                        blackboard);
                }
                if (bestPreferred)
                {
                    actor.Brain.RetirePreferredBranchAfterHardFailure(
                        candidate.Branch,
                        failure,
                        CharacterAiPreferredActionFailureSource
                            .JobGiverCandidateCommit);
                    failureSummary = failure.ToString();
                    return CharacterAiDecisionRules.Result(
                        false,
                        branchOverride,
                        taskName,
                        failureSummary,
                        blackboard);
                }
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
            return CharacterAiDecisionRules.Result(
                runResult.Handled,
                branchOverride,
                taskName,
                runResult.Handled
                    ? GetActionLabel(actor.Brain.bestAction?.actionset)
                    : runResult.Status,
                blackboard);
        }

        failureSummary = lastFailure.HasFailure
            ? lastFailure.ToString()
            : "No valid utility candidates.";
        return CharacterAiDecisionRules.Result(
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
        if (CharacterNeedAiThresholds.IsEmergencyOrImminentPhysicalHarm(
                actor,
                CharacterCondition.HUNGER))
        {
            AddUniqueJobGiver(catalog.GetFood);
        }

        if (CharacterNeedAiThresholds.IsEmergencyOrImminentPhysicalHarm(
                actor,
                CharacterCondition.THIRST))
        {
            AddUniqueJobGiver(catalog.Drink);
        }

        if (CharacterNeedAiThresholds.IsEmergency(
                actor,
                CharacterCondition.EXCRETION))
        {
            AddUniqueJobGiver(catalog.Toilet);
        }

        if (CharacterNeedAiThresholds.IsEmergency(
                actor,
                CharacterCondition.HYGIENE))
        {
            AddUniqueJobGiver(catalog.Hygiene);
        }

        if (CharacterNeedAiThresholds.IsEmergency(
                actor,
                CharacterCondition.SLEEP)
            || context.InjuryUrgency > 0.35f
            || context.HealthUrgency > 0.45f)
        {
            AddUniqueJobGiver(catalog.Rest);
        }

        if (!context.IsWorker)
        {
            AddUniqueJobGiver(catalog.ExitDungeon);
        }

        return jobGiverBuffer;
    }

    private IReadOnlyList<CharacterAiJobGiver> BuildRoutineJobGivers(
        CharacterActor actor,
        in CharacterAiDecisionContext context)
    {
        ICharacterAiJobGiverCatalog catalog = RequireJobGiverCatalog(actor);
        jobGiverBuffer.Clear();
        AddUniqueJobGiver(catalog.GetFood);
        AddUniqueJobGiver(catalog.Drink);
        AddUniqueJobGiver(catalog.Toilet);
        AddUniqueJobGiver(catalog.Hygiene);
        AddUniqueJobGiver(catalog.Rest);
        AddUniqueJobGiver(catalog.Recreation);

        if (context.IsWorker)
        {
            AddUniqueJobGiver(catalog.Work);
            if (context.IsOffDuty)
            {
                AddUniqueJobGiver(catalog.Shopping);
                AddUniqueJobGiver(catalog.LookAround);
            }
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
            CharacterAiBranch.Drink => CharacterAiBranch.SurvivalNeeds,
            CharacterAiBranch.Rest => CharacterAiBranch.SurvivalNeeds,
            CharacterAiBranch.Toilet => CharacterAiBranch.SurvivalNeeds,
            CharacterAiBranch.Hygiene => CharacterAiBranch.SurvivalNeeds,
            CharacterAiBranch.ExitDungeon => CharacterAiBranch.SurvivalNeeds,
            CharacterAiBranch.Work => CharacterAiBranch.DutyWork,
            CharacterAiBranch.LeisureVisit => CharacterAiBranch.LeisureVisit,
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

    public CharacterAiDecisionTickResult ClearContinueMacro(CharacterActor actor) =>
        macroDecisions.ClearContinue(actor);

    public CharacterAiDecisionTickResult RunComplainMacro(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal) =>
        macroDecisions.RunComplain(actor, blackboard, goal);

    public CharacterAiDecisionTickResult ApplyAvoidFacility(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal) =>
        macroDecisions.ApplyAvoidFacility(actor, blackboard, goal);

    public CharacterAiDecisionTickResult RunExitDungeonMacro(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal) =>
        macroDecisions.RunExitDungeon(actor, blackboard, goal);

    public CharacterAiDecisionTickResult RunVandalizeMacro(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal) =>
        macroDecisions.RunVandalize(actor, blackboard, goal);

    public static bool MatchesFacility(BuildableObject building, int id, string tag) =>
        building != null
        && ((id >= 0 && building.id == id) || building.HasSemanticTag(tag));

    private static ICharacterAiJobGiverCatalog RequireJobGiverCatalog(CharacterActor actor)
    {
        if (actor == null || actor.Brain == null)
        {
            throw new InvalidOperationException(
                $"{nameof(CharacterAiDecisionPipeline)} requires an actor with {nameof(AIBrain)} for job giver lookup.");
        }

        return actor.Brain.RequireJobGiverCatalog();
    }

    public static CharacterAiBranch GetBranchForActionSet(AIActionSet actionSet) =>
        actionSet?.Branch ?? CharacterAiBranch.None;

    public static string GetActionLabel(AIActionSet actionSet) =>
        actionSet != null ? actionSet.GetDisplayLabel() : "None";

}
