using UnityEngine;

public abstract class AIPrimitiveSurvivalAction : AIActionSet
{
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
            // Crossing the emergency line does not instantly invalidate an
            // already reachable authored queue. Keep a five-point safety band
            // in which a short reservation wait is preferable to an inferior
            // primitive action. At the hard fallback line the actor bypasses
            // occupancy so a long queue cannot create a death spiral.
            if (FacilityCandidateScorer.HasReachableQueueableCandidate(
                    actor,
                    searchResult,
                    facilityRole)
                && actor.Stats != null
                && actor.Stats.TryGetConditionValue(condition, out float value))
            {
                float hardFallback = Mathf.Max(
                    0f,
                    actor.Stats.GetNeedResponse(condition).emergencyStart - 10f);
                return value <= hardFallback;
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
