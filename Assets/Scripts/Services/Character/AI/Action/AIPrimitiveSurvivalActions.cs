using UnityEngine;

public abstract class AIPrimitiveSurvivalAction : AIActionSet
{
    public override bool RequiresDestination => false;
    public override int InterruptPriority => 45;

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
        return Mathf.Clamp01(Mathf.Max(baseScore, utility) * 0.65f);
    }

    /// <summary>
    /// Primitive self-care is a true fallback.  At routine urgency an actor
    /// waits for the facility index and uses an authored facility whenever the
    /// role exists.  At emergency urgency an unusable/occupied facility may be
    /// bypassed so the actor cannot enter a deprivation death spiral.
    /// </summary>
    protected static bool CanUsePrimitiveFallback(
        CharacterActor actor,
        FacilityRole facilityRole,
        CharacterCondition condition)
    {
        if (actor == null)
        {
            return false;
        }

        // Coarse role presence, or even an indexed building on this grid, may
        // still be outside the actor's reachable component. Only an
        // actor-reachable authored fixture suppresses the primitive fallback.
        // A temporarily occupied reachable fixture still remains a candidate
        // (IsCandidate deliberately does not consume a visit slot), so it keeps
        // the actor on the wait/retry path without mistaking global role bits
        // for a usable local service.
        GridPathSearchResult searchResult = actor.Brain?.GetPathSearch(actor);
        if (searchResult != null
            && FacilityCandidateScorer.HasCandidate(
                actor,
                searchResult,
                facilityRole))
        {
            return false;
        }

        // No reachable facility exists. This remains available at both routine
        // and emergency urgency so a new settlement can survive before it
        // builds its first fixture or while an isolated fixture is unreachable.
        return true;
    }
}
