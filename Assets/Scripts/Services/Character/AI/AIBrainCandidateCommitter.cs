internal static class AIBrainCandidateCommitter
{
    public static bool TryPrepareCommit(
        CharacterActor actor,
        AIAction[] availableActions,
        AIBrainActionEvaluator evaluator,
        in CharacterAiActionCandidate candidate,
        out AIAction action,
        out AIActionFailure failure)
    {
        failure = AIActionFailure.None;
        action = candidate.Action;
        if (action?.actionset == null)
        {
            failure = candidate.Failure.HasFailure
                ? candidate.Failure
                : AIActionFailure.Create(
                    AIActionFailureKind.NoAction,
                    "JobGiver candidate has no action.");
            actor?.Blackboard?.ReportActionFailure(null, failure);
            return false;
        }

        if (actor == null
            || !actor.CanRunAi
            || availableActions == null
            || availableActions.Length == 0)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.NoAction,
                "No available AI actions.");
            actor?.Blackboard?.ReportActionFailure(action.actionset, failure);
            return false;
        }

        if (System.Array.IndexOf(availableActions, action) < 0)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.NoAction,
                "JobGiver candidate is not registered in AIBrain.");
            actor.Blackboard?.ReportActionFailure(action.actionset, failure);
            return false;
        }

        // Candidate selection already performed CanStart, destination
        // resolution, and consideration scoring during this root decision.
        // Re-evaluating here repeated the entire facility scan (including
        // proximity/world-signal queries) immediately before committing the
        // exact same candidate. Reuse that decision-local evaluation and let
        // SetResolvedDestinationWithFailure revalidate mutable physical state
        // such as destruction, path availability, and reservation ownership.
        bool hasEvaluation = evaluator.TryGetCached(
            action,
            out AIBrainActionEvaluation evaluation);
        if (!hasEvaluation
            && !evaluator.TryEvaluate(actor, action, out evaluation))
        {
            failure = evaluation.Failure;
            actor.Blackboard?.ReportActionFailure(action.actionset, failure);
            return false;
        }

        if (hasEvaluation && !evaluation.CanConsider)
        {
            failure = evaluation.Failure;
            actor.Blackboard?.ReportActionFailure(action.actionset, failure);
            return false;
        }

        BuildableObject evaluatedDestination =
            candidate.Destination ?? evaluation.Destination;
        if (!action.actionset.RevalidateBeforeCommit(
                actor,
                evaluatedDestination,
                out failure))
        {
            evaluator.RemoveEvaluation(action);
            actor.Blackboard?.ReportActionFailure(action.actionset, failure);
            return false;
        }

        evaluator.RemoveEvaluation(action);

        // This is the single BT winner's execution preparation, not a candidate
        // scan. Ordinary scans may have exhausted this frame's budget; use the
        // broker's bounded, fair continuation allowance so preparation can finish.
        // Selection and Blackboard.Commit still wait for a valid path/reservation.
        if (action.SetResolvedDestinationWithFailure(
            actor,
            evaluatedDestination,
            out failure,
            GridPathSearchPriority.Urgent))
        {
            return true;
        }

        actor.Blackboard?.ReportActionFailure(action.actionset, failure);
        return false;
    }
}
