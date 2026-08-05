using System;

internal sealed class CharacterAiMacroDecisionRunner
{
    private readonly Func<
        CharacterActor,
        string,
        CharacterAiBranch,
        CharacterAiDecisionTickResult> runSelectedAction;

    public CharacterAiMacroDecisionRunner(
        Func<CharacterActor, string, CharacterAiBranch,
            CharacterAiDecisionTickResult> runSelectedAction)
    {
        this.runSelectedAction = runSelectedAction
            ?? throw new ArgumentNullException(nameof(runSelectedAction));
    }

    public CharacterAiDecisionTickResult RunGoal(CharacterActor actor)
    {
        if (!TryPrepare(actor, out CharacterBlackboard blackboard, out string error))
        {
            return Result(false, "Run Macro Goal", error, blackboard);
        }

        CharacterMacroGoal goal = blackboard.ActiveMacroGoal;
        if (goal == null || !blackboard.HasActiveMacroGoal())
        {
            return Result(false, "Run Macro Goal", "No active macro goal.", blackboard);
        }

        return goal.type switch
        {
            CharacterMacroGoalType.Continue => ClearContinue(actor),
            CharacterMacroGoalType.SeekFood => RunJobGiver(
                actor,
                blackboard,
                goal,
                "Seek Food",
                RequireJobGiverCatalog(actor).GetFood),
            CharacterMacroGoalType.SeekToilet => RunJobGiver(
                actor,
                blackboard,
                goal,
                "Seek Toilet",
                RequireJobGiverCatalog(actor).Toilet),
            CharacterMacroGoalType.SeekHygiene => RunJobGiver(
                actor,
                blackboard,
                goal,
                "Seek Hygiene",
                RequireJobGiverCatalog(actor).Hygiene),
            CharacterMacroGoalType.SeekFun => RunJobGiver(
                actor,
                blackboard,
                goal,
                "Seek Fun",
                RequireJobGiverCatalog(actor).Shopping,
                RequireJobGiverCatalog(actor).LookAround),
            CharacterMacroGoalType.AvoidFacility => ApplyAvoidFacility(
                actor, blackboard, goal),
            CharacterMacroGoalType.Complain => RunComplain(actor, blackboard, goal),
            CharacterMacroGoalType.ExitDungeon => RunExitDungeon(actor, blackboard, goal),
            CharacterMacroGoalType.Vandalize => RunVandalize(actor, blackboard, goal),
            _ => Result(
                false,
                "Run Macro Goal",
                $"Unsupported macro goal: {goal.type}.",
                blackboard)
        };
    }

    public CharacterAiDecisionTickResult ClearContinue(CharacterActor actor)
    {
        CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
        if (blackboard == null || !blackboard.HasActiveMacroGoal())
        {
            return Result(false, "ContinueMacro", "No active macro goal.", blackboard);
        }

        blackboard.ClearMacroGoal("Macro goal requested Continue.");
        return Result(false, "ContinueMacro", "Continue.", blackboard);
    }

    public CharacterAiDecisionTickResult RunComplain(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal)
    {
        if (!TryPrepare(actor, out blackboard, out string error))
        {
            return Result(false, "Complain", error, blackboard);
        }

        goal ??= blackboard.ActiveMacroGoal;
        if (goal == null)
        {
            return Result(false, "Complain", "Macro goal is missing.", blackboard);
        }

        actor.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Social,
            CharacterActivityOutcomes.Responded,
            $"불만을 표현함: {goal.reason}",
            actionId: "macro:complain",
            reasonCode: goal.reason,
            sentiment: -0.65f,
            bubbleEligible: true));
        blackboard.ClearMacroGoal("Complain emitted.");
        return Result(true, "Complain", "Complain.", blackboard);
    }

    public CharacterAiDecisionTickResult ApplyAvoidFacility(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal)
    {
        if (!TryPrepare(actor, out blackboard, out string error))
        {
            return Result(false, "AvoidFacility", error, blackboard);
        }

        if (goal == null)
        {
            return Result(false, "AvoidFacility", "Macro goal is missing.", blackboard);
        }

        BuildableObject target = FindFacility(
            actor,
            goal.targetFacilityId,
            goal.targetFacilityTag);
        if (target == null)
        {
            blackboard.ClearMacroGoal("AvoidFacility target not found.");
            return Result(
                false,
                "AvoidFacility",
                "Target facility not found.",
                blackboard);
        }

        blackboard.PutFacilityOnCooldown(target, goal.reason);
        blackboard.ClearMacroGoal("AvoidFacility cooldown applied.");
        return Result(true, "AvoidFacility", target.name, blackboard);
    }

    public CharacterAiDecisionTickResult RunExitDungeon(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal)
    {
        if (!TryPrepare(actor, out blackboard, out string error))
        {
            return Result(false, "ExitDungeon", error, blackboard);
        }

        if (goal == null)
        {
            return Result(false, "ExitDungeon", "Macro goal is missing.", blackboard);
        }

        if (!actor.TryGetAbility(out AbilityMove move))
        {
            return Result(false, "ExitDungeon", "AbilityMove is missing.", blackboard);
        }

        if (CharacterWorkRoleUtility.TryGetWork(actor, out _))
        {
            blackboard.ClearMacroGoal(
                "Workers cannot exit through ordinary mood macros.");
            return Result(
                false,
                "ExitDungeon",
                "Worker exit is handled by staff systems.",
                blackboard);
        }

        actor.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Lifecycle,
            CharacterActivityOutcomes.Departed,
            $"던전을 떠나기로 함: {goal.reason}",
            actionId: "macro:exit-dungeon",
            reasonCode: goal.reason,
            sentiment: -0.8f,
            bubbleEligible: true));
        blackboard.ClearCommitment(
            CharacterAiInterruptReason.MacroGoalChanged,
            "ExitDungeon macro.");
        blackboard.ClearMacroGoal("ExitDungeon started.");
        actor.Brain.isBestActionEnd = false;
        move.StartExitDungeon();
        return Result(true, "ExitDungeon", "Exit started.", blackboard);
    }

    public CharacterAiDecisionTickResult RunVandalize(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal)
    {
        if (!TryPrepare(actor, out blackboard, out string error))
        {
            return Result(false, "Vandalize", error, blackboard);
        }

        if (goal == null)
        {
            return Result(false, "Vandalize", "Macro goal is missing.", blackboard);
        }

        BuildableObject target = FindFacility(
            actor,
            goal.targetFacilityId,
            goal.targetFacilityTag);
        if (target == null)
        {
            blackboard.ClearMacroGoal("Vandalize target not found.");
            actor.AddActivity(CharacterActivityEvent.InternalAi(
                CharacterActivityOutcomes.Failed,
                "vandalize-target-missing",
                $"AI macro vandalize failed: target not found - {goal.reason}"));
            return Result(
                false,
                "Vandalize",
                "Target facility not found.",
                blackboard);
        }

        if (!CanVandalize(target, out string failureReason))
        {
            blackboard.ClearMacroGoal($"Vandalize target rejected: {failureReason}");
            actor.AddActivity(CharacterActivityEvent.InternalAi(
                CharacterActivityOutcomes.Failed,
                "vandalize-target-rejected",
                $"AI macro vandalize failed: {failureReason}"));
            return Result(false, "Vandalize", failureReason, blackboard);
        }

        target.SetDamaged(true);
        blackboard.ClearCommitment(
            CharacterAiInterruptReason.MacroGoalChanged,
            "Vandalize macro executed.");
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
        return Result(true, "Vandalize", GetBuildingLabel(target), blackboard);
    }

    private CharacterAiDecisionTickResult RunJobGiver(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal,
        string label,
        params CharacterAiJobGiver[] jobGivers)
    {
        string taskName = $"Macro {label} JobGiver";
        if (!TryPrepare(actor, out blackboard, out string error))
        {
            return Result(false, taskName, error, blackboard);
        }

        if (goal == null || !blackboard.HasActiveMacroGoal())
        {
            return Result(false, taskName, "Macro goal is missing.", blackboard);
        }

        CharacterAiJobCandidate bestCandidate = default;
        bool hasCandidate = false;
        string lastFailure = "No JobGiver candidates.";
        foreach (CharacterAiJobGiver jobGiver in jobGivers ?? Array.Empty<CharacterAiJobGiver>())
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
            }
            else
            {
                lastFailure = candidate.DebugSummary;
            }
        }

        if (!hasCandidate)
        {
            blackboard.ClearMacroGoal(
                $"{label} macro could not find a JobGiver candidate: {lastFailure}");
            return Result(false, taskName, lastFailure, blackboard);
        }

        blackboard.RecordSelectedJobGiverUtility(bestCandidate);
        if (!actor.Brain.TryCommitActionCandidate(
                bestCandidate.ActionCandidate,
                out AIActionFailure failure))
        {
            blackboard.ReportActionFailure(null, failure);
            blackboard.ClearMacroGoal(
                $"{label} macro could not commit candidate: {failure}");
            return Result(false, taskName, failure.ToString(), blackboard);
        }

        CharacterAiDecisionTickResult runResult = runSelectedAction(
            actor,
            $"Run {label} Macro Action",
            CharacterAiBranch.MacroGoal);
        string status = $"{runResult.Status} | {bestCandidate.DebugSummary}";
        blackboard.ClearMacroGoal(
            runResult.Handled
                ? (!string.IsNullOrWhiteSpace(goal.reason)
                    ? goal.reason
                    : $"{label} macro consumed.")
                : $"{label} macro action failed: {runResult.Status}");
        return Result(
            runResult.Handled,
            $"Run {label} Macro Action",
            status,
            blackboard);
    }

    private static BuildableObject FindFacility(CharacterActor actor, int id, string tag)
    {
        return RequireFacilityLookup(actor).FindFacility(id, tag);
    }

    private static ICharacterAiFacilityLookup RequireFacilityLookup(CharacterActor actor)
    {
        if (actor == null || actor.Brain == null)
        {
            throw new InvalidOperationException(
                $"{nameof(CharacterAiMacroDecisionRunner)} requires an actor with "
                + $"{nameof(AIBrain)} for facility lookup.");
        }

        return actor.Brain.RequireFacilityLookup();
    }

    private static ICharacterAiJobGiverCatalog RequireJobGiverCatalog(
        CharacterActor actor)
    {
        if (actor == null || actor.Brain == null)
        {
            throw new InvalidOperationException(
                $"{nameof(CharacterAiMacroDecisionRunner)} requires an actor with "
                + $"{nameof(AIBrain)} for job giver lookup.");
        }

        return actor.Brain.RequireJobGiverCatalog();
    }

    private static bool CanVandalize(BuildableObject target, out string failureReason)
    {
        failureReason = string.Empty;
        if (target == null)
        {
            failureReason = "Target facility is missing.";
        }
        else if (target.isDestroy)
        {
            failureReason = "Target facility is destroyed.";
        }
        else if (target.IsDamaged)
        {
            failureReason = "Target facility is already damaged.";
        }
        else if (target.IsGridMovement)
        {
            failureReason = "Movement buildings cannot be vandalized.";
        }
        else if (target.Facility == null)
        {
            failureReason = "Target is not a facility.";
        }

        return string.IsNullOrEmpty(failureReason);
    }

    private static string GetBuildingLabel(BuildableObject building)
    {
        if (building == null)
        {
            return "None";
        }

        return building.BuildingData != null
            && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
                ? building.BuildingData.objectName
                : building.name;
    }

    private static bool TryPrepare(
        CharacterActor actor,
        out CharacterBlackboard blackboard,
        out string error)
    {
        blackboard = actor != null ? actor.Blackboard : null;
        if (actor == null)
        {
            error = "Actor is missing.";
            return false;
        }

        if (blackboard == null)
        {
            error = "CharacterBlackboard is missing.";
            return false;
        }

        if (!actor.CanRunAi)
        {
            error = $"AI cannot run in state {actor.CurrentLifecycleState}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static CharacterAiDecisionTickResult Result(
        bool handled,
        string task,
        string status,
        CharacterBlackboard blackboard)
    {
        blackboard?.RecordBtStatus(CharacterAiBranch.MacroGoal, task, status);
        return new CharacterAiDecisionTickResult(
            handled,
            CharacterAiBranch.MacroGoal,
            task,
            status);
    }
}
