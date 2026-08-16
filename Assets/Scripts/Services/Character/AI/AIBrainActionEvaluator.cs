using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using Unity.Profiling;

internal sealed class AIBrainActionEvaluator
{
    private static readonly ProfilerMarker ActionPrepareMarker =
        new ProfilerMarker("CharacterAi.ActionPrepare");
    private static readonly ProfilerMarker ActionConsiderationsMarker =
        new ProfilerMarker("CharacterAi.ActionConsiderations");

    private readonly IGameClock clock;
    private readonly ICharacterAiPerformanceRecorder performanceRecorder;
    private readonly Dictionary<AIAction, AIBrainActionEvaluation> evaluations =
        new Dictionary<AIAction, AIBrainActionEvaluation>();
    private readonly Dictionary<AIActionSet, float> cooldownUntil =
        new Dictionary<AIActionSet, float>();
    private readonly Dictionary<(AIActionSet ActionSet, int DestinationId), float>
        destinationCooldownUntil =
            new Dictionary<(AIActionSet ActionSet, int DestinationId), float>();

    public AIBrainActionEvaluator(
        IGameClock clock,
        ICharacterAiPerformanceRecorder performanceRecorder)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.performanceRecorder = performanceRecorder
            ?? throw new ArgumentNullException(nameof(performanceRecorder));
    }

    public void ClearEvaluations()
    {
        evaluations.Clear();
    }

    public void RemoveEvaluation(AIAction action)
    {
        if (action != null)
        {
            evaluations.Remove(action);
        }
    }

    public bool TryGetCached(
        AIAction action,
        out AIBrainActionEvaluation evaluation)
    {
        if (action != null && evaluations.TryGetValue(action, out evaluation))
        {
            return true;
        }

        evaluation = default;
        return false;
    }

    public void ClearCooldowns()
    {
        cooldownUntil.Clear();
        destinationCooldownUntil.Clear();
    }

    public void ClearCooldown(
        AIActionSet actionSet,
        BuildableObject destination)
    {
        if (actionSet == null)
        {
            return;
        }

        // A successful action proves only that this action and, when present,
        // this exact destination are usable.  Do not erase cooldowns authored
        // by unrelated failures elsewhere in the decision graph.
        cooldownUntil.Remove(actionSet);
        if (!ReferenceEquals(destination, null))
        {
            destinationCooldownUntil.Remove(
                (actionSet, destination.GetInstanceID()));
        }
    }

    public void StartCooldown(AIActionSet actionSet, float durationSeconds)
    {
        if (actionSet != null)
        {
            cooldownUntil[actionSet] = clock.Time + Math.Max(0.1f, durationSeconds);
        }
    }

    public void StartDestinationCooldown(
        AIActionSet actionSet,
        BuildableObject destination,
        float durationSeconds)
    {
        // Unity's overloaded null reports a destroyed component as null even
        // though its managed identity is still available.  Destruction is the
        // most important destination-scoped failure, so use CLR null here.
        if (actionSet == null || ReferenceEquals(destination, null))
        {
            return;
        }

        destinationCooldownUntil[(actionSet, destination.GetInstanceID())] =
            clock.Time + Math.Max(0.1f, durationSeconds);
    }

    public bool TryEvaluate(
        CharacterActor actor,
        AIAction action,
        out AIBrainActionEvaluation evaluation)
    {
        CharacterAiDecisionContext context = default;
        return TryEvaluate(actor, action, false, in context, out evaluation);
    }

    public bool TryEvaluate(
        CharacterActor actor,
        AIAction action,
        in CharacterAiDecisionContext context,
        out AIBrainActionEvaluation evaluation)
    {
        return TryEvaluate(actor, action, true, in context, out evaluation);
    }

    public bool CanConsider(
        CharacterActor actor,
        AIAction action,
        out AIActionFailure failure)
    {
        return CanConsider(actor, action, out failure, out _);
    }

    public bool CanConsider(
        CharacterActor actor,
        AIAction action,
        out AIActionFailure failure,
        out BuildableObject destination)
    {
        CharacterAiDecisionContext context = default;
        return CanConsider(actor, action, false, in context, out failure, out destination);
    }

    public bool CanUse(
        CharacterActor actor,
        AIAction action,
        out AIActionFailure failure)
    {
        if (!CanConsider(actor, action, out failure))
        {
            return false;
        }

        if (action.SetDestinationWithFailure(actor, out failure))
        {
            return true;
        }

        failure = RefineFailure(actor, action, failure);
        return false;
    }

    private bool TryEvaluate(
        CharacterActor actor,
        AIAction action,
        bool hasDecisionContext,
        in CharacterAiDecisionContext context,
        out AIBrainActionEvaluation evaluation)
    {
        if (action != null && evaluations.TryGetValue(action, out evaluation))
        {
            return evaluation.CanConsider;
        }

        bool canConsider = CanConsider(
            actor,
            action,
            hasDecisionContext,
            in context,
            out AIActionFailure failure,
            out BuildableObject destination);
        evaluation = new AIBrainActionEvaluation(canConsider, failure, destination);
        if (action != null && !failure.IsDeferred)
        {
            evaluations[action] = evaluation;
        }

        return canConsider;
    }

    private bool CanConsider(
        CharacterActor actor,
        AIAction action,
        bool hasDecisionContext,
        in CharacterAiDecisionContext context,
        out AIActionFailure failure,
        out BuildableObject destination)
    {
        destination = null;
        failure = AIActionFailure.None;
        if (action?.actionset == null)
        {
            failure = AIActionFailure.Create(AIActionFailureKind.NoAction, "Action is missing.");
            return false;
        }

        if (IsCoolingDown(action.actionset))
        {
            failure = AIActionFailure.Create(AIActionFailureKind.Cooldown);
            return false;
        }

        long prepareStarted = performanceRecorder.DetailedCollectionEnabled
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        long prepareAllocatedAtStart = prepareStarted != 0L
            ? GC.GetAllocatedBytesForCurrentThread()
            : 0L;
        bool prepared;
        using (ActionPrepareMarker.Auto())
        {
            prepared = hasDecisionContext
                ? action.actionset.TryPrepareCandidate(
                    actor,
                    in context,
                    null,
                    out destination,
                    out failure)
                : action.actionset.TryPrepareCandidate(
                    actor,
                    null,
                    out destination,
                    out failure);
        }

        RecordPerformance(
            AiPerformanceCategory.ActionPrepare,
            "prepare",
            actor,
            action.actionset,
            prepareStarted,
            prepareAllocatedAtStart);
        if (prepared && IsDestinationCoolingDown(action.actionset, destination))
        {
            BuildableObject coolingDestination = destination;
            if (!TrySelectAvailableAlternateDestination(
                    actor,
                    action.actionset,
                    out destination))
            {
                failure = AIActionFailure.Create(
                    AIActionFailureKind.Cooldown,
                    "This destination is cooling down after a recent runtime failure.",
                    coolingDestination);
                return false;
            }
        }
        if (!prepared)
        {
            if (!failure.HasFailure)
            {
                failure = AIActionFailure.Create(AIActionFailureKind.CannotStart);
            }

            failure = RefineFailure(actor, action, failure);
            return false;
        }

        long scoreStarted = performanceRecorder.DetailedCollectionEnabled
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        long scoreAllocatedAtStart = scoreStarted != 0L
            ? GC.GetAllocatedBytesForCurrentThread()
            : 0L;
        float actionScore;
        using (ActionConsiderationsMarker.Auto())
        {
            actionScore = hasDecisionContext
                ? action.CalculateScore(actor, in context)
                : action.CalculateScore(actor);
        }

        RecordPerformance(
            AiPerformanceCategory.ActionConsiderationScore,
            "considerations",
            actor,
            action.actionset,
            scoreStarted,
            scoreAllocatedAtStart);
        if (actionScore <= 0f)
        {
            failure = AIActionFailure.Create(AIActionFailureKind.NoScore);
            return false;
        }

        return true;
    }

    private bool IsCoolingDown(AIActionSet actionSet)
    {
        return actionSet != null
            && cooldownUntil.TryGetValue(actionSet, out float until)
            && clock.Time < until;
    }

    private bool IsDestinationCoolingDown(
        AIActionSet actionSet,
        BuildableObject destination)
    {
        return actionSet != null
            && !ReferenceEquals(destination, null)
            && destinationCooldownUntil.TryGetValue(
                (actionSet, destination.GetInstanceID()),
                out float until)
            && clock.Time < until;
    }

    private bool TrySelectAvailableAlternateDestination(
        CharacterActor actor,
        AIActionSet actionSet,
        out BuildableObject destination)
    {
        destination = null;
        IReadOnlyList<BuildableObject> candidates =
            actionSet?.GetDestinationCandidates(actor, null);
        if (candidates == null || candidates.Count == 0)
        {
            return false;
        }

        // This allocation occurs only after a destination has failed and is
        // cooling down.  The normal scoring hot path remains allocation-free.
        List<BuildableObject> available = new List<BuildableObject>(candidates.Count);
        for (int index = 0; index < candidates.Count; index++)
        {
            BuildableObject candidate = candidates[index];
            if (candidate == null
                || candidate.isDestroy
                || IsDestinationCoolingDown(actionSet, candidate))
            {
                continue;
            }

            available.Add(candidate);
        }

        destination = actionSet.SelectDestination(actor, available);
        return destination != null
            && !destination.isDestroy
            && !IsDestinationCoolingDown(actionSet, destination);
    }

    private void RecordPerformance(
        AiPerformanceCategory category,
        string traceKind,
        CharacterActor actor,
        AIActionSet actionSet,
        long started,
        long allocatedAtStart)
    {
        if (started == 0L)
        {
            return;
        }

        double elapsedMilliseconds =
            (System.Diagnostics.Stopwatch.GetTimestamp() - started)
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency;
        performanceRecorder.Record(
            category,
            elapsedMilliseconds,
            Math.Max(
                0L,
                GC.GetAllocatedBytesForCurrentThread() - allocatedAtStart));
        performanceRecorder.RecordSlowOperation(
            traceKind,
            actor,
            actionSet,
            null,
            elapsedMilliseconds);
    }

    private static AIActionFailure RefineFailure(
        CharacterActor actor,
        AIAction action,
        AIActionFailure failure)
    {
        if (action?.actionset != null
            && action.actionset.HasSemanticTag(CharacterAiActionTags.Work)
            && actor != null
            && actor.TryGetAbility(out AbilityWork work)
            && work.TryGetLastRejectedWorkCandidate(out WorkTargetCandidate candidate)
            && candidate.Building != null)
        {
            BuildableObject building =
                WorkTargetCandidateRuntimeAdapter.ResolveBuilding(candidate);
            return AIActionFailure.Create(
                candidate.FailureKind != AIActionFailureKind.None
                    ? candidate.FailureKind
                    : failure.Kind,
                candidate.FailureReason,
                building);
        }

        return failure;
    }
}
