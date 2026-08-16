using UnityEngine;

public static class CharacterNeedAiThresholds
{
    // Primitive food and water actions include pathing, reservation and use
    // time. Escalate before physical damage begins when care cannot complete
    // before the need crosses that band.
    // Primitive survival may have to cross the unbuilt starting map to reach
    // the first warehouse stack. Ninety game-seconds is a conservative
    // bootstrap forecast; authored facilities use
    // their own exact route ETA instead of this conservative fallback bound.
    private const float SurvivalCareCompletionHorizonSeconds = 90f;
    private static readonly CharacterCondition[] SurvivalRoutineConditions =
    {
        CharacterCondition.HUNGER,
        CharacterCondition.THIRST,
        CharacterCondition.SLEEP,
        CharacterCondition.EXCRETION,
        CharacterCondition.HYGIENE
    };

    public static float GetRoutineUtility(
        CharacterActor actor,
        CharacterCondition condition)
    {
        if (actor?.Stats == null
            || !actor.Stats.TryGetConditionValue(condition, out float value))
        {
            return 0f;
        }

        CharacterNeedResponseProfile response =
            actor.Stats.GetNeedResponse(condition);
        if (value > response.routineStart)
        {
            return 0f;
        }

        float urgency = Mathf.InverseLerp(
            response.routineStart,
            response.emergencyStart,
            value);
        return Mathf.Lerp(0.35f, 1f, urgency);
    }

    public static float GetFacilityRoutineUtility(
        CharacterActor actor,
        FacilityRole roles)
    {
        const FacilityRole routineRoles = FacilityRole.Toilet
            | FacilityRole.Hygiene
            | FacilityRole.Entertainment;
        if (roles == FacilityRole.None || (roles & ~routineRoles) != 0)
        {
            return FacilityCandidateScorer.GetNeedScore(actor, roles);
        }

        float utility = 0f;
        Accumulate(FacilityRole.Toilet, CharacterCondition.EXCRETION);
        Accumulate(FacilityRole.Hygiene, CharacterCondition.HYGIENE);
        Accumulate(FacilityRole.Entertainment, CharacterCondition.FUN);
        return utility;

        void Accumulate(FacilityRole role, CharacterCondition condition)
        {
            if ((roles & role) == 0)
            {
                return;
            }

            utility = Mathf.Max(utility, GetRoutineUtility(actor, condition));
        }
    }

    public static bool IsEmergency(
        CharacterActor actor,
        CharacterCondition condition)
    {
        return actor?.Stats != null
            && actor.Stats.TryGetConditionValue(condition, out float value)
            && value <= actor.Stats.GetNeedResponse(condition).emergencyStart;
    }

    public static bool IsEmergencyOrImminentPhysicalHarm(
        CharacterActor actor,
        CharacterCondition condition)
    {
        if (condition is not CharacterCondition.HUNGER
            and not CharacterCondition.THIRST)
        {
            return IsEmergency(actor, condition);
        }

        if (actor?.Stats == null
            || !actor.Stats.TryGetConditionValue(condition, out float value))
        {
            return false;
        }

        float projected = value - actor.Stats.GetExpectedTimedNeedLoss(
            condition,
            SurvivalCareCompletionHorizonSeconds);
        return projected < 20f;
    }

    public static CharacterActionIntentKind GetEmergencyIntentKind(
        CharacterActor actor,
        CharacterCondition condition)
    {
        if (condition is CharacterCondition.HUNGER or CharacterCondition.THIRST)
        {
            if (actor?.Stats == null
                || !actor.Stats.TryGetConditionValue(condition, out float value))
            {
                return CharacterActionIntentKind.EmergencyNeed;
            }

            if (value < 10f)
            {
                return CharacterActionIntentKind.EmergencyPhysicalCritical;
            }

            return value < 20f
                ? CharacterActionIntentKind.EmergencyPhysicalActive
                : CharacterActionIntentKind.EmergencyPhysicalImminent;
        }

        return condition switch
        {
            CharacterCondition.SLEEP => CharacterActionIntentKind.EmergencySleep,
            CharacterCondition.EXCRETION => CharacterActionIntentKind.EmergencyExcretion,
            CharacterCondition.HYGIENE => CharacterActionIntentKind.EmergencyHygiene,
            _ => CharacterActionIntentKind.EmergencyNeed
        };
    }

    public static bool IsSatisfied(
        CharacterActor actor,
        CharacterCondition condition)
    {
        return actor?.Stats != null
            && actor.Stats.TryGetConditionValue(condition, out float value)
            && value >= actor.Stats.GetNeedResponse(condition).resumeTarget;
    }

    public static bool TryGetMostUrgentSurvivalRoutineNeed(
        CharacterActor actor,
        out CharacterCondition condition,
        out float utility,
        out float value,
        out float routineStart)
    {
        condition = default;
        utility = 0f;
        value = 0f;
        routineStart = 0f;
        if (actor?.Stats == null)
        {
            return false;
        }

        for (int index = 0; index < SurvivalRoutineConditions.Length; index++)
        {
            CharacterCondition candidate = SurvivalRoutineConditions[index];
            float candidateUtility = GetRoutineUtility(actor, candidate);
            if (candidateUtility <= utility
                || !actor.Stats.TryGetConditionValue(candidate, out float candidateValue))
            {
                continue;
            }

            condition = candidate;
            utility = candidateUtility;
            value = candidateValue;
            routineStart = actor.Stats.GetNeedResponse(candidate).routineStart;
        }

        return utility > 0f;
    }
}
