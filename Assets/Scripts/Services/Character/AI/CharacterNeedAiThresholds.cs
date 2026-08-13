using UnityEngine;

public static class CharacterNeedAiThresholds
{
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
