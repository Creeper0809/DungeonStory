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

        if (FacilityCandidateScorer.HasUsableCandidate(actor, facilityRole))
        {
            return false;
        }

        // Role presence (and an index still being built) is enough to keep the
        // actor on the authored facility path.  A temporarily occupied,
        // cooling-down or reserved fixture must create a wait/retry, not turn
        // into an outdoor latrine or bucket wash next to a working bathroom.
        // Emergency urgency raises the facility job's priority; it does not
        // redefine an existing fixture as absent.
        if (FacilityCandidateScorer.HasCandidate(actor, null, facilityRole))
        {
            return false;
        }

        // Only a genuinely absent facility role permits the primitive path.
        // This remains available at both routine and emergency urgency so a
        // new settlement can survive before it builds its first fixture.
        return true;
    }
}
