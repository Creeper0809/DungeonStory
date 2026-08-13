using UnityEngine;

public abstract class AIPrimitiveSurvivalAction : AIActionSet
{
    private const float PrimitiveFallbackCriticalNeedFloor = 5f;
    public override bool RequiresDestination => false;
    public override int InterruptPriority => 45;

    public override bool RevalidateBeforeCommit(
        CharacterActor actor,
        BuildableObject evaluatedDestination,
        out AIActionFailure failure)
    {
        if (CanStart(actor))
        {
            failure = AIActionFailure.None;
            return true;
        }

        failure = AIActionFailure.Create(
            AIActionFailureKind.CannotStart,
            "Primitive fallback became invalid before commit.");
        return false;
    }

    protected bool RevalidateAtExecution(CharacterActor actor)
    {
        if (CanStart(actor))
        {
            return true;
        }

        actor?.AddActivity(CharacterActivityEvent.InternalAi(
            CharacterActivityOutcomes.Cancelled,
            "primitive-stale-execution-blocked",
            $"Primitive fallback cancelled at execution because authored service became available: action={GetType().Name}."));
        actor?.Brain?.RequestImmediateReplan(clearFailures: true);
        return false;
    }

    protected static float PrimitiveScore(
        CharacterActor actor,
        CharacterCondition condition,
        float baseScore)
    {
        if (CharacterNeedAiThresholds.IsEmergency(actor, condition))
        {
            return 1f;
        }

        float utility = CharacterNeedAiThresholds.GetRoutineUtility(actor, condition);
        // Keep the primitive option unattractive at the first routine hint,
        // but never attenuate the continuous need urgency itself. The previous
        // max(...)*0.65 formula capped every non-emergency need at 0.65, making
        // founders knowingly postpone food until the emergency discontinuity.
        return Mathf.Clamp01(Mathf.Max(baseScore * 0.65f, utility));
    }

    /// <summary>
    /// Primitive self-care is a true fallback.  At routine urgency an actor
    /// waits for the facility index and uses an authored facility whenever the
    /// role exists.  At emergency urgency an unusable/occupied facility may be
    /// bypassed so the actor cannot enter a deprivation death spiral.
    /// </summary>
    public static bool CanUsePrimitiveFallback(
        CharacterActor actor,
        FacilityRole facilityRole,
        CharacterCondition condition)
    {
        if (actor == null)
        {
            return false;
        }

        AIBrain brain = actor.Brain;
        GridPathSearchResult searchResult = brain?.GetPathSearch(actor);
        if (searchResult == null)
        {
            // A deferred path query is an explicit Pending state, not evidence
            // that infrastructure is absent. Wait for the broker instead of
            // committing a primitive action that may run for several seconds.
            return brain?.IsPathSearchDeferred != true;
        }

        bool hasImmediateFacility = FacilityCandidateScorer.HasCandidate(
            actor,
            searchResult,
            facilityRole);
        if (hasImmediateFacility)
        {
            return false;
        }

        if (CharacterNeedAiThresholds.IsEmergency(actor, condition))
        {
            // An occupied facility is not the same as absent infrastructure.
            // Compare the need that will remain after travel, queue waves, and
            // the actor's own service time. Primitive fallback is allowed only
            // when that real wait would cross the critical floor.
            if (FacilityCandidateScorer.TryGetMinimumQueueableServiceEta(
                    actor,
                    searchResult,
                    facilityRole,
                    out float serviceEta)
                && actor.Stats != null
                && actor.Stats.TryGetConditionValue(condition, out float value))
            {
                float projectedValue = value
                    - actor.Stats.GetExpectedTimedNeedLoss(
                        condition,
                        serviceEta);
                return projectedValue <= PrimitiveFallbackCriticalNeedFloor;
            }

            return true;
        }

        // At routine urgency a reachable, structurally valid facility remains
        // the correct plan even while its capacity is temporarily occupied.
        return !FacilityCandidateScorer.HasReachableQueueableCandidate(
            actor,
            searchResult,
            facilityRole);
    }
}
