public enum CharacterAiPreferredActionDisposition
{
    None = 0,
    Deferred = 1,
    Selected = 2
}

public enum CharacterAiPreferredActionFailureSource
{
    None = 0,
    DirectActionEvaluation = 1,
    JobGiverActionEvaluation = 2,
    JobGiverCandidateCommit = 3,
    BehaviorTaskActionEvaluation = 4
}