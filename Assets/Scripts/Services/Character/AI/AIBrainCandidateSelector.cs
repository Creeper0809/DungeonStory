using System;
using System.Collections.Generic;
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
    // A root decision evaluates several JobGivers in one pass.  Each giver has
    // a different predicate, so a single shared continuation was repeatedly
    // discarded before later actions (notably recreation) could be reached.
    // Keep one bounded continuation per active predicate instead.
    private readonly List<AIBrainActionScoringContinuation> continuations =
        new List<AIBrainActionScoringContinuation>();
#if UNITY_EDITOR
    private bool deterministicFullPassForDiagnostics;
    private bool logisticsMeasurementOnlyForDiagnostics;
#endif

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

    public bool IsPending => continuations.Count > 0;

#if UNITY_EDITOR
    public void ConfigureDeterministicFullPassForDiagnostics(bool enabled)
    {
        deterministicFullPassForDiagnostics = enabled;
        continuations.Clear();
    }

    public bool LogisticsMeasurementOnlyForDiagnostics =>
        logisticsMeasurementOnlyForDiagnostics;

    public void ConfigureLogisticsMeasurementForDiagnostics(bool enabled)
    {
        logisticsMeasurementOnlyForDiagnostics = enabled;
        continuations.Clear();
    }
#endif

    public bool IsPendingFor(
        Predicate<AIActionSet> predicate,
        bool hasDecisionContext)
    {
        return FindContinuation(predicate, hasDecisionContext) != null;
    }

    public void Reset()
    {
        continuations.Clear();
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

        AIBrainActionScoringContinuation continuation =
            FindContinuation(predicate, hasDecisionContext);
        if (continuation == null)
        {
            continuation = new AIBrainActionScoringContinuation();
            continuation.Reset(predicate, hasDecisionContext);
            continuations.Add(continuation);
        }

        double sliceMilliseconds = scheduling.GetDecisionWorkSliceMilliseconds(actor);
        long sliceStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        int processedThisSlice = 0;
        while (continuation.NextActionIndex < actions.Length)
        {
            AIAction action = actions[continuation.NextActionIndex];
            if (action?.actionset == null
                || !IsAllowedForDiagnostics(action.actionset)
                || !predicate(action.actionset))
            {
                continuation.NextActionIndex++;
                continue;
            }

            // Predicate filtering is an O(1) array read and does not execute
            // destination, path, reservation or score logic. Counting skipped
            // actions against the work slice made a destinationless action near
            // the end of the shared action array report PathSearchDeferred for
            // several ticks. Only real candidate evaluations consume this
            // selector's decision budget.
            processedThisSlice++;

            AIBrainActionEvaluation evaluation;
            bool canConsider = hasDecisionContext
                ? evaluator.TryEvaluate(actor, action, in context, out evaluation)
                : evaluator.TryEvaluate(actor, action, out evaluation);
            if (action == null)
            {
                continuation.NextActionIndex++;
                continue;
            }
            if (!canConsider)
            {
                action.score = 0f;
                if (FailurePriority(evaluation.Failure.Kind)
                    > FailurePriority(continuation.BestFailure.Kind))
                {
                    continuation.BestFailure = evaluation.Failure;
                }

                if (evaluation.Failure.IsDeferred)
                {
                    break;
                }

                continuation.NextActionIndex++;
                if (ShouldYield(sliceStarted, sliceMilliseconds, processedThisSlice))
                {
                    SkipNonMatchingActions(
                        actions,
                        predicate,
                        continuation);
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
                SkipNonMatchingActions(
                    actions,
                    predicate,
                    continuation);
                break;
            }
        }

        if (continuation.NextActionIndex < actions.Length)
        {
            AIActionFailure pending = AIActionFailure.Create(
                AIActionFailureKind.CandidateEvaluationDeferred,
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
        continuations.Remove(continuation);
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

    private AIBrainActionScoringContinuation FindContinuation(
        Predicate<AIActionSet> predicate,
        bool hasDecisionContext)
    {
        for (int index = 0; index < continuations.Count; index++)
        {
            AIBrainActionScoringContinuation candidate = continuations[index];
            if (candidate.Matches(predicate, hasDecisionContext))
            {
                return candidate;
            }
        }

        return null;
    }

    private void SkipNonMatchingActions(
        AIAction[] actions,
        Predicate<AIActionSet> predicate,
        AIBrainActionScoringContinuation continuation)
    {
        while (continuation.NextActionIndex < actions.Length)
        {
            AIAction action = actions[continuation.NextActionIndex];
            if (action?.actionset != null
                && IsAllowedForDiagnostics(action.actionset)
                && predicate(action.actionset))
            {
                return;
            }

            continuation.NextActionIndex++;
        }
    }

    private bool IsAllowedForDiagnostics(AIActionSet actionSet)
    {
#if UNITY_EDITOR
        if (logisticsMeasurementOnlyForDiagnostics)
        {
            return actionSet is AIHaul or AIWait;
        }
#endif
        return true;
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

    private bool ShouldYield(long started, double sliceMilliseconds, int processedCount)
    {
#if UNITY_EDITOR
        if (deterministicFullPassForDiagnostics)
        {
            return false;
        }
#endif
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
            AIActionFailureKind.CandidateEvaluationDeferred => 10,
            AIActionFailureKind.FacilityCandidateDeferred => 10,
            AIActionFailureKind.PathSearchDeferred => 10,
            AIActionFailureKind.Cooldown => 5,
            AIActionFailureKind.NoScore => 1,
            _ => 0
        };
    }
}
