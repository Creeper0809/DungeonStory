public interface ICharacterAiDiagnosticsQuery
{
    double LastProcessingMilliseconds { get; }
    int LastPathSearchCount { get; }
    int CurrentPathSearchBudget { get; }
    float GetNextDecisionDelay(CharacterActor actor);
    CharacterAiDecisionScheduleObservation CaptureDecisionSchedule(
        CharacterActor actor);
}
