using System;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;

internal sealed class AIBrainCandidateSelector
{
    private static readonly ProfilerMarker ActionScoringMarker =
        new ProfilerMarker("CharacterAi.ActionScoring");

    private readonly AIBrainActionEvaluator evaluator;
    private readonly ICharacterAiSchedulingService scheduling;
    private readonly ICharacterAiPerformanceRecorder performanceRecorder;
    private readonly AIBrainActionScoringContinuation reusableContinuation =
        new AIBrainActionScoringContinuation();
    private AIBrainActionScoringContinuation continuation;

    public AIBrainCandidateSelector(
        AIBrainActionEvaluator evaluator,
        ICharacterAiSchedulingService scheduling,
        ICharacterAiPerformanceRecorder performanceRecorder)
    {
        this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        this.scheduling = scheduling ?? throw new ArgumentNullException(nameof(scheduling));
        this.performanceRecorder = performanceRecorder
            ?? throw new ArgumentNullException(nameof(performanceRecorder));
    }

    public bool IsPending => continuation != null;

    public void Reset()
    {
        continuation = null;
    }

    public bool TryFind(
        CharacterActor actor,
        AIAction[] actions,
        Predicate<AIActionSet> predicate,
        Func<AIAction, float> selectionScore,
        Action clearRelatedCaches,
        out CharacterAiActionCandidate candidate)
    {
        CharacterAiDecisionContext context = default;
        CharacterAiActionCandidate result = default;
        bool found = Measure(() => TryFindCore(
            actor,
            actions,
            predicate,
            false,
            in context,
            selectionScore,
            clearRelatedCaches,
            out result));
        candidate = result;
        return found;
    }

    public bool TryFind(
        CharacterActor actor,
        AIAction[] actions,
        Predicate<AIActionSet> predicate,
        in CharacterAiDecisionContext context,
        Func<AIAction, float> selectionScore,
        Action clearRelatedCaches,
        out CharacterAiActionCandidate candidate)
    {
        CharacterAiDecisionContext contextCopy = context;
        CharacterAiActionCandidate result = default;
        bool found = Measure(() => TryFindCore(
            actor,
            actions,
            predicate,
            true,
            in contextCopy,
            selectionScore,
            clearRelatedCaches,
            out result));
        candidate = result;
        return found;
    }

    private bool TryFindCore(
        CharacterActor actor,
        AIAction[] actions,
        Predicate<AIActionSet> predicate,
        bool hasDecisionContext,
        in CharacterAiDecisionContext context,
        Func<AIAction, float> selectionScore,
        Action clearRelatedCaches,
        out CharacterAiActionCandidate candidate)
    {
        candidate = default;
        if (predicate == null)
        {
            candidate = FailureCandidate(
                actor,
                AIActionFailure.Create(AIActionFailureKind.NoAction, "Action predicate is missing."));
            return false;
        }

        if (actor == null || !actor.CanRunAi || actions == null || actions.Length == 0)
        {
            candidate = FailureCandidate(
                actor,
                AIActionFailure.Create(AIActionFailureKind.NoAction, "No available AI actions."));
            return false;
        }

        if (continuation != null && !continuation.Matches(predicate, hasDecisionContext))
        {
            evaluator.ClearEvaluations();
            clearRelatedCaches?.Invoke();
            continuation = null;
        }

        if (continuation == null)
        {
            reusableContinuation.Reset(predicate, hasDecisionContext);
            continuation = reusableContinuation;
        }

        double sliceMilliseconds = scheduling.GetDecisionWorkSliceMilliseconds(actor);
        long sliceStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        int processedThisSlice = 0;
        while (continuation.NextActionIndex < actions.Length)
        {
            AIAction action = actions[continuation.NextActionIndex];
            processedThisSlice++;
            if (action?.actionset == null || !predicate(action.actionset))
            {
                continuation.NextActionIndex++;
                if (ShouldYield(sliceStarted, sliceMilliseconds, processedThisSlice))
                {
                    break;
                }

                continue;
            }

            AIBrainActionEvaluation evaluation;
            bool canConsider = hasDecisionContext
                ? evaluator.TryEvaluate(actor, action, in context, out evaluation)
                : evaluator.TryEvaluate(actor, action, out evaluation);
            if (!canConsider)
            {
                action.score = 0f;
                if (FailurePriority(evaluation.Failure.Kind)
                    > FailurePriority(continuation.BestFailure.Kind))
                {
                    continuation.BestFailure = evaluation.Failure;
                }

                if (evaluation.Failure.Kind == AIActionFailureKind.PathSearchDeferred)
                {
                    break;
                }

                continuation.NextActionIndex++;
                if (ShouldYield(sliceStarted, sliceMilliseconds, processedThisSlice))
                {
                    break;
                }

                continue;
            }

            continuation.NextActionIndex++;
            float score = selectionScore(action);
            if (continuation.BestCandidate == null || score > continuation.BestScore)
            {
                continuation.BestCandidate = action;
                continuation.BestScore = score;
            }

            if (ShouldYield(sliceStarted, sliceMilliseconds, processedThisSlice))
            {
                break;
            }
        }

        if (continuation.NextActionIndex < actions.Length)
        {
            AIActionFailure pending = AIActionFailure.Create(
                AIActionFailureKind.PathSearchDeferred,
                "Action candidate evaluation is waiting for the next work slice.");
            candidate = new CharacterAiActionCandidate(
                null,
                0f,
                pending,
                actor.ShouldCollectDetailedAiDiagnostics
                    ? $"{continuation.NextActionIndex}/{actions.Length} candidates evaluated"
                    : string.Empty);
            return false;
        }

        AIAction bestCandidate = continuation.BestCandidate;
        float bestScore = continuation.BestScore;
        AIActionFailure bestFailure = continuation.BestFailure;
        continuation = null;
        if (bestCandidate == null)
        {
            candidate = FailureCandidate(actor, bestFailure);
            return false;
        }

        float resolvedScore = Mathf.Max(0f, bestScore);
        candidate = new CharacterAiActionCandidate(
            bestCandidate,
            resolvedScore,
            AIActionFailure.None,
            actor.ShouldCollectDetailedAiDiagnostics
                ? $"{bestCandidate.actionset.GetDisplayLabel()} score={resolvedScore:0.###}"
                : string.Empty,
            evaluator.TryGetCached(bestCandidate, out AIBrainActionEvaluation bestEvaluation)
                ? bestEvaluation.Destination
                : null);
        return resolvedScore > 0f;
    }

    private bool Measure(Func<bool> action)
    {
        bool collect = performanceRecorder.DetailedCollectionEnabled;
        long started = collect ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
        long allocatedAtStart = collect ? GC.GetAllocatedBytesForCurrentThread() : 0L;
        try
        {
            using (ActionScoringMarker.Auto())
            {
                return action();
            }
        }
        finally
        {
            if (started != 0L)
            {
                performanceRecorder.Record(
                    AiPerformanceCategory.ActionScoring,
                    (System.Diagnostics.Stopwatch.GetTimestamp() - started)
                    * 1000.0
                    / System.Diagnostics.Stopwatch.Frequency,
                    Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - allocatedAtStart));
            }
        }
    }

    private static CharacterAiActionCandidate FailureCandidate(
        CharacterActor actor,
        AIActionFailure failure)
    {
        return new CharacterAiActionCandidate(
            null,
            0f,
            failure,
            actor?.ShouldCollectDetailedAiDiagnostics == true
                ? failure.ToString()
                : string.Empty);
    }

    private static bool ShouldYield(long started, double sliceMilliseconds, int processedCount)
    {
        if (processedCount <= 0)
        {
            return false;
        }

        double elapsed =
            (System.Diagnostics.Stopwatch.GetTimestamp() - started)
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency;
        return elapsed >= Math.Max(0.05, sliceMilliseconds);
    }

    private static int FailurePriority(AIActionFailureKind kind)
    {
        return kind switch
        {
            AIActionFailureKind.DestinationOccupied => 80,
            AIActionFailureKind.NoDestination => 60,
            AIActionFailureKind.DestinationSelectionFailed => 55,
            AIActionFailureKind.NoWork => 45,
            AIActionFailureKind.OffDuty => 35,
            AIActionFailureKind.Unsupported => 25,
            AIActionFailureKind.CannotStart => 20,
            AIActionFailureKind.PathSearchDeferred => 10,
            AIActionFailureKind.Cooldown => 5,
            AIActionFailureKind.NoScore => 1,
            _ => 0
        };
    }
}
