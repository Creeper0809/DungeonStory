using System;

public interface ICharacterAiSchedulingService
{
    bool IsDrivingAi { get; }
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

public interface ICharacterAiDiagnosticsQuery
{
    double LastProcessingMilliseconds { get; }
    int LastPathSearchCount { get; }
    int CurrentPathSearchBudget { get; }
    float GetNextDecisionDelay(CharacterActor actor);
}

public interface ICharacterMoodImpulseQuery
{
    CharacterMoodImpulseType LastAppliedType { get; }
    string LastAppliedActorName { get; }
}

public sealed class CharacterAiSchedulingService :
    ICharacterAiSchedulingService,
    ICharacterAiDiagnosticsQuery
{
    private readonly CharacterSceneRuntimeReferences runtimeReferences;

    public CharacterAiSchedulingService(
        CharacterSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public bool IsDrivingAi => ResolveScheduler().IsDrivingAi;
    public double LastProcessingMilliseconds =>
        ResolveScheduler().LastProcessingMilliseconds;
    public int LastPathSearchCount => ResolveScheduler().LastPathSearchCount;
    public int CurrentPathSearchBudget =>
        ResolveScheduler().CurrentPathSearchBudget;

    public void Register(CharacterActor actor)
    {
        ResolveScheduler().RegisterActor(actor);
    }

    public void Unregister(CharacterActor actor)
    {
        if (TryResolveScheduler(out CharacterAiScheduler resolvedScheduler))
        {
            resolvedScheduler.UnregisterActor(actor);
        }
    }

    public void RequestImmediateDecision(CharacterActor actor)
    {
        ResolveScheduler().RequestImmediateDecisionFor(actor);
    }

    public bool TryConsumePathSearchBudget()
    {
        return ResolveScheduler().TryConsumePathSearchBudget();
    }

    public bool ShouldShowCharacterFeedback(CharacterActor actor)
    {
        return TryResolveScheduler(out CharacterAiScheduler resolvedScheduler)
            && resolvedScheduler.ShouldShowCharacterFeedbackFor(actor);
    }

    public bool ShouldCollectDetailedDiagnostics(CharacterActor actor)
    {
        return TryResolveScheduler(out CharacterAiScheduler resolvedScheduler)
            && resolvedScheduler.ShouldCollectDetailedDiagnosticsFor(actor);
    }

    public int GetMovementFrameStride(CharacterActor actor)
    {
        return ResolveScheduler().GetMovementFrameStrideFor(actor);
    }

    public double GetDecisionWorkSliceMilliseconds(CharacterActor actor)
    {
        return ResolveScheduler().GetDecisionWorkSliceMillisecondsFor(actor);
    }

    public void ResetPathSearchBudgetForDebug()
    {
        ResolveScheduler().ResetPathSearchBudgetForDebugInstance();
    }

    public float GetNextDecisionDelay(CharacterActor actor)
    {
        return ResolveScheduler().GetNextDecisionDelayForDebug(actor);
    }

    private CharacterAiScheduler ResolveScheduler()
    {
        return TryResolveScheduler(out CharacterAiScheduler resolvedScheduler)
            ? resolvedScheduler
            : throw new InvalidOperationException($"{nameof(ICharacterAiSchedulingService)} requires a loaded {nameof(CharacterAiScheduler)}.");
    }

    private bool TryResolveScheduler(out CharacterAiScheduler resolvedScheduler)
    {
        resolvedScheduler = runtimeReferences.AiScheduler;
        return resolvedScheduler != null;
    }
}

public sealed class CharacterMoodImpulseQuery :
    ICharacterMoodImpulseQuery
{
    private readonly CharacterSceneRuntimeReferences runtimeReferences;

    public CharacterMoodImpulseQuery(
        CharacterSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public CharacterMoodImpulseType LastAppliedType =>
        runtimeReferences.AiDirector != null
            ? runtimeReferences.AiDirector.LastAppliedMoodImpulseType
            : CharacterMoodImpulseType.None;

    public string LastAppliedActorName =>
        runtimeReferences.AiDirector != null
            ? runtimeReferences.AiDirector.LastAppliedMoodImpulseActorName
            : string.Empty;
}
