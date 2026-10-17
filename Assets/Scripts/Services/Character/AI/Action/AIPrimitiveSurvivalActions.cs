using UnityEngine;

public abstract class AIPrimitiveSurvivalAction : AIActionSet
{
    private const float PrimitiveFallbackCriticalNeedFloor = 0.5f;
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

        if (actor != null)
        {
            actor.PublishAiDiagnostic(new CharacterAiDiagnosticEvent(
                actor,
                CharacterAiDiagnosticTransition.PrimitiveStaleExecutionBlocked,
                "primitive-stale-execution-blocked",
                GetType().Name));
        }
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
        return CanUsePrimitiveFallback(
            actor,
            facilityRole,
            condition,
            out _);
    }

    internal static bool CanUsePrimitiveFallback(
        CharacterActor actor,
        FacilityRole facilityRole,
        CharacterCondition condition,
        out bool pathSearchPending)
    {
        pathSearchPending = false;
        if (actor == null)
        {
            return false;
        }

        AIBrain brain = actor.Brain;
        GridPathSearchResult searchResult = brain?.GetPathSearch(actor);
        if (searchResult == null)
        {
            pathSearchPending = true;
            // No published path snapshot is a Pending state regardless of
            // whether the broker has set its deferred bit yet. The previous
            // `!IsPathSearchDeferred` fallback opened a one-frame gap after a
            // cache clear: deprivation Tick committed a primitive action, then
            // the synchronous started-event diagnostic obtained the new search
            // and found an immediately usable authored facility.
            return false;
        }

        bool hasImmediateFacility = FacilityCandidateScorer.HasCandidate(
            actor,
            searchResult,
            facilityRole);
        if (hasImmediateFacility)
        {
            return false;
        }

        if (CharacterNeedAiThresholds.IsEmergencyOrImminentPhysicalHarm(actor, condition))
        {
            // Match deprivation dispatch and running lease urgency: an
            // unbounded delivery request must not delay imminent physical care.
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
        bool hasAuthoredPipeline = facilityRole == FacilityRole.Meal
            ? FacilityCandidateScorer
                .HasReachableQueueableCandidateIncludingPendingMealDelivery(
                    actor,
                    searchResult,
                    facilityRole)
            : FacilityCandidateScorer.HasReachableQueueableCandidate(
                actor,
                searchResult,
                facilityRole);
        return !hasAuthoredPipeline;
    }
}
