public interface ICharacterAiSchedulingService
{
    bool IsDrivingAi { get; }
    bool IsSchedulerAvailable { get; }
    void Register(CharacterActor actor);
    void Unregister(CharacterActor actor);
    void RequestImmediateDecision(CharacterActor actor);
    bool TryConsumePathSearchBudget();
    bool ShouldShowCharacterFeedback(CharacterActor actor);
    bool ShouldCollectDetailedDiagnostics(CharacterActor actor);
    int GetMovementFrameStride(CharacterActor actor);
    double GetDecisionWorkSliceMilliseconds(CharacterActor actor);
    void ResetPathSearchBudgetForDebug();
}
