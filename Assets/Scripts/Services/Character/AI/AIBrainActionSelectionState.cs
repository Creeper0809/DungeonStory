using System;

internal readonly struct AIBrainActionEvaluation
{
    public AIBrainActionEvaluation(
        bool canConsider,
        AIActionFailure failure,
        BuildableObject destination)
    {
        CanConsider = canConsider;
        Failure = failure;
        Destination = destination;
    }

    public bool CanConsider { get; }
    public AIActionFailure Failure { get; }
    public BuildableObject Destination { get; }
}

internal sealed class AIBrainActionScoringContinuation
{
    public Predicate<AIActionSet> Predicate;
    public bool HasDecisionContext;
    public int NextActionIndex;
    public AIAction BestCandidate;
    public float BestScore = float.MinValue;
    public AIActionFailure BestFailure = AIActionFailure.Create(
        AIActionFailureKind.NoAction,
        "일치하는 AI 행동이 없습니다.");

    public void Reset(
        Predicate<AIActionSet> predicate,
        bool hasDecisionContext)
    {
        Predicate = predicate;
        HasDecisionContext = hasDecisionContext;
        NextActionIndex = 0;
        BestCandidate = null;
        BestScore = float.MinValue;
        BestFailure = AIActionFailure.Create(
            AIActionFailureKind.NoAction,
            "선택할 수 있는 AI 행동이 없습니다.");
    }

    public bool Matches(
        Predicate<AIActionSet> predicate,
        bool hasDecisionContext)
    {
        if (HasDecisionContext != hasDecisionContext
            || Predicate == null
            || predicate == null)
        {
            return false;
        }

        return ReferenceEquals(Predicate, predicate)
            || (ReferenceEquals(Predicate.Target, predicate.Target)
                && Predicate.Method == predicate.Method);
    }
}
