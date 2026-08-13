using System;

internal static class AIBrainActionContinuationPolicy
{
    public static bool TryFindInterrupt(
        CharacterActor actor,
        AIAction runningAction,
        AIAction[] availableActions,
        AIBrainActionEvaluator evaluator,
        Func<AIAction, float> selectionScore,
        out AIAction interruptAction,
        out string interruptReason)
    {
        interruptAction = null;
        interruptReason = string.Empty;
        if (runningAction?.actionset == null || availableActions == null)
        {
            return false;
        }

        int runningPriority = runningAction.actionset.InterruptPriority;
        float bestScore = float.MinValue;
        int bestPriority = int.MinValue;
        foreach (AIAction action in availableActions)
        {
            if (action?.actionset == null
                || action == runningAction
                || action.actionset.InterruptPriority <= runningPriority)
            {
                continue;
            }

            if (!evaluator.CanUse(actor, action, out _))
            {
                action.ReleaseReservation(actor);
                continue;
            }

            int priority = action.actionset.InterruptPriority;
            float score = selectionScore(action);
            if (priority > bestPriority || (priority == bestPriority && score > bestScore))
            {
                if (interruptAction != null && interruptAction != action)
                {
                    interruptAction.ReleaseReservation(actor);
                }

                bestPriority = priority;
                bestScore = score;
                interruptAction = action;
            }
            else
            {
                action.ReleaseReservation(actor);
            }
        }

        if (interruptAction == null)
        {
            return false;
        }

        interruptReason = $"Higher-priority action: {interruptAction.actionset.GetDisplayLabel()}";
        return true;
    }

    public static bool ShouldStopForReplan(
        CharacterActor actor,
        AIAction runningAction,
        float minimumPersistenceSeconds,
        out string stopReason)
    {
        stopReason = string.Empty;
        if (actor == null || runningAction?.actionset == null)
        {
            return false;
        }

        AIActionSet actionSet = runningAction.actionset;
        if (!actionSet.IsContinuous || !runningAction.HasStarted)
        {
            return false;
        }

        if (!actionSet.CanContinue(actor, runningAction, out stopReason))
        {
            stopReason = string.IsNullOrWhiteSpace(stopReason)
                ? "Current action can no longer continue."
                : stopReason;
            return true;
        }

        if (runningAction.RunningSeconds < minimumPersistenceSeconds)
        {
            return false;
        }

        // A character already resolving a concrete self-care need must finish
        // that short transaction before mood impulses or another routine need
        // can replace it. Crisis actions still pre-empt through the separate
        // higher InterruptPriority path in AIBrain.TryFindInterruptAction.
        if (actionSet.HasSemanticTag(CharacterAiActionTags.SelfCare))
        {
            return false;
        }

        if (CharacterMoodImpulseUtility.ShouldInterruptCurrentAction(
            actor,
            runningAction,
            out stopReason))
        {
            if (actor.Blackboard != null
                && !actor.Blackboard.CanBreakCommitment(
                    CharacterAiInterruptReason.MoodImpulseChanged))
            {
                stopReason = "Commitment prevents a mood-driven interruption.";
                return false;
            }

            return true;
        }

        if (!actionSet.CanInterrupt(actor, runningAction, out stopReason))
        {
            return false;
        }

        stopReason = string.IsNullOrWhiteSpace(stopReason)
            ? "Current action requested interruption."
            : stopReason;
        if (actor.Blackboard != null
            && !actor.Blackboard.CanBreakCommitment(
                CharacterAiInterruptReason.CurrentActionStopped))
        {
            stopReason = "Commitment prevents the requested action interruption.";
            return false;
        }

        return true;
    }

    public static bool CanInterruptForSurvival(
        CharacterActor actor,
        AIAction runningAction,
        float minimumPersistenceSeconds,
        out string interruptReason)
    {
        interruptReason = string.Empty;
        AIActionSet actionSet = runningAction?.actionset;
        if (actor == null
            || actionSet == null
            || !actionSet.IsContinuous
            || !runningAction.HasStarted
            || !actionSet.AllowsSurvivalEmergencyInterrupt
            || actionSet.HasSemanticTag(CharacterAiActionTags.SelfCare)
            || runningAction.RunningSeconds < minimumPersistenceSeconds)
        {
            return false;
        }

        if (actor.Blackboard != null
            && !actor.Blackboard.CanBreakCommitment(
                CharacterAiInterruptReason.SurvivalEmergency))
        {
            return false;
        }

        interruptReason = "Survival emergency interrupts the current action.";
        return true;
    }

    public static bool CanContinue(
        CharacterActor actor,
        AIAction runningAction,
        float minimumPersistenceSeconds,
        Func<AIActionSet, string> actionLabel,
        out string status)
    {
        status = string.Empty;
        if (actor == null || runningAction?.actionset == null)
        {
            status = "No running action.";
            return false;
        }

        AIActionSet actionSet = runningAction.actionset;
        if (!actionSet.IsContinuous)
        {
            status = $"{actionLabel(actionSet)} is not continuous.";
            return false;
        }

        if (!runningAction.HasStarted)
        {
            status = $"{actionLabel(actionSet)} has not started.";
            return false;
        }

        if (!actionSet.CanContinue(actor, runningAction, out string stopReason))
        {
            status = string.IsNullOrWhiteSpace(stopReason)
                ? "Current action can no longer continue."
                : stopReason;
            return false;
        }

        bool preservesSelfCareLifecycle =
            actionSet.HasSemanticTag(CharacterAiActionTags.SelfCare);
        if (!preservesSelfCareLifecycle
            && runningAction.RunningSeconds >= minimumPersistenceSeconds
            && CharacterMoodImpulseUtility.ShouldInterruptCurrentAction(
                actor,
                runningAction,
                out string moodReason))
        {
            if (actor.Blackboard != null
                && !actor.Blackboard.CanBreakCommitment(
                    CharacterAiInterruptReason.MoodImpulseChanged))
            {
                status = "Commitment is being maintained.";
                return true;
            }

            status = moodReason;
            return false;
        }

        if (!preservesSelfCareLifecycle
            && runningAction.RunningSeconds >= minimumPersistenceSeconds
            && actionSet.CanInterrupt(actor, runningAction, out string interruptReason))
        {
            status = string.IsNullOrWhiteSpace(interruptReason)
                ? "Current action requested interruption."
                : interruptReason;
            if (actor.Blackboard != null
                && !actor.Blackboard.CanBreakCommitment(
                    CharacterAiInterruptReason.CurrentActionStopped))
            {
                status = "Commitment is being maintained.";
                return true;
            }

            return false;
        }

        status = $"{actionLabel(actionSet)} running {runningAction.RunningSeconds:0.0}s";
        return true;
    }
}
