using UnityEngine;

public static class CharacterNeedAiThresholds
{
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
}
