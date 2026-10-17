using System;
using UnityEngine;

[Serializable]
public class CharacterAiPersonality
{
    [Range(0.25f, 2f)] public float diligence = 1f;
    [Range(0.25f, 2f)] public float curiosity = 1f;
    [Range(0.25f, 2f)] public float selfCare = 1f;
    [Range(0.25f, 2f)] public float patience = 1f;
    [Range(0.25f, 2f)] public float shoppingInterest = 1f;
    [Range(0.25f, 2f)] public float sociability = 1f;
    [Range(0.25f, 2f)] public float riskTaking = 1f;
    [Range(0.25f, 2f)] public float orderliness = 1f;
    [Range(0.25f, 2f)] public float outdoorPreference = 1f;
    [Range(0.25f, 2f)] public float noveltySeeking = 1f;
    [Range(0.25f, 2f)] public float routineAdherence = 1f;

    public float GetRoutineMultiplier(CharacterAiIntentionType intention)
    {
        return ClampMultiplier(intention switch
        {
            CharacterAiIntentionType.Survive => selfCare,
            CharacterAiIntentionType.Recover => (selfCare + patience) * 0.5f,
            CharacterAiIntentionType.Work => (diligence + routineAdherence + orderliness) / 3f,
            CharacterAiIntentionType.Logistics => (diligence + orderliness) * 0.5f,
            CharacterAiIntentionType.Guard => (diligence + riskTaking + patience) / 3f,
            CharacterAiIntentionType.Hunt => (riskTaking + outdoorPreference + diligence) / 3f,
            CharacterAiIntentionType.Leisure => (curiosity + noveltySeeking) * 0.5f,
            CharacterAiIntentionType.Social => sociability,
            CharacterAiIntentionType.Shop => shoppingInterest,
            CharacterAiIntentionType.Exit => Mathf.Max(0.25f, 2f - patience),
            CharacterAiIntentionType.Idle => patience,
            _ => 1f
        });
    }

    public float GetRiskTolerance01()
    {
        return Mathf.InverseLerp(0.25f, 2f, riskTaking);
    }

    internal static float ClampMultiplier(float value)
    {
        return Mathf.Clamp(value, 0.25f, 2f);
    }
}
