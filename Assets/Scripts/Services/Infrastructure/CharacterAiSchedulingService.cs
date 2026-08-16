using System;

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

    public bool IsDrivingAi =>
        TryResolveScheduler(out CharacterAiScheduler scheduler)
        && scheduler.IsDrivingAi;
    public bool IsSchedulerAvailable =>
        TryResolveScheduler(out _);
    public double LastProcessingMilliseconds =>
        TryResolveScheduler(out CharacterAiScheduler scheduler)
            ? scheduler.LastProcessingMilliseconds
            : 0d;
    public int LastPathSearchCount =>
        TryResolveScheduler(out CharacterAiScheduler scheduler)
            ? scheduler.LastPathSearchCount
            : 0;
    public int CurrentPathSearchBudget =>
        TryResolveScheduler(out CharacterAiScheduler scheduler)
            ? scheduler.CurrentPathSearchBudget
            : 0;

    public void Register(CharacterActor actor)
    {
        if (TryResolveScheduler(out CharacterAiScheduler scheduler))
        {
            scheduler.RegisterActor(actor);
        }
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
        if (TryResolveScheduler(out CharacterAiScheduler scheduler))
        {
            scheduler.RequestImmediateDecisionFor(actor);
        }
    }

    public bool TryConsumePathSearchBudget()
    {
        return TryResolveScheduler(out CharacterAiScheduler scheduler)
            && scheduler.TryConsumePathSearchBudget();
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
        return TryResolveScheduler(out CharacterAiScheduler scheduler)
            ? scheduler.GetMovementFrameStrideFor(actor)
            : 1;
    }

    public double GetDecisionWorkSliceMilliseconds(CharacterActor actor)
    {
        return TryResolveScheduler(out CharacterAiScheduler scheduler)
            ? scheduler.GetDecisionWorkSliceMillisecondsFor(actor)
            : 0d;
    }

    public void ResetPathSearchBudgetForDebug()
    {
        if (TryResolveScheduler(out CharacterAiScheduler scheduler))
        {
            scheduler.ResetPathSearchBudgetForDebugInstance();
        }
    }

    public float GetNextDecisionDelay(CharacterActor actor)
    {
        return TryResolveScheduler(out CharacterAiScheduler scheduler)
            ? scheduler.GetNextDecisionDelayForDebug(actor)
            : 0f;
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
