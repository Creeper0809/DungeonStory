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

        evaluator.RemoveEvaluation(action);
        if (!evaluator.TryEvaluate(actor, action, out AIBrainActionEvaluation evaluation))
        {
            failure = evaluation.Failure;
            actor.Blackboard?.ReportActionFailure(action.actionset, failure);
            return false;
        }

        if (action.SetResolvedDestinationWithFailure(
            actor,
            candidate.Destination ?? evaluation.Destination,
            out failure))
        {
            return true;
        }

        actor.Blackboard?.ReportActionFailure(action.actionset, failure);
        return false;
    }
}
