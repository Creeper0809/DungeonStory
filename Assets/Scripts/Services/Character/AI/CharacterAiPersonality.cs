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

    public float GetActionMultiplier(AIActionSet actionSet)
    {
        if (actionSet != null && actionSet.HasSemanticTag(CharacterAiActionTags.Work))
        {
            return ClampMultiplier(diligence);
        }
        if (actionSet != null && actionSet.HasSemanticTag(CharacterAiActionTags.Curiosity))
        {
            return ClampMultiplier(curiosity);
        }
        if (actionSet != null && actionSet.HasSemanticTag(CharacterAiActionTags.SelfCare))
        {
            return ClampMultiplier(selfCare);
        }
        if (actionSet != null && actionSet.HasSemanticTag(CharacterAiActionTags.Shopping))
        {
            return ClampMultiplier(shoppingInterest);
        }
        if (actionSet != null && actionSet.HasSemanticTag(CharacterAiActionTags.Patience))
        {
            return ClampMultiplier(patience);
        }

        return 1f;
    }

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

    private static float ClampMultiplier(float value)
    {
        return Mathf.Clamp(value, 0.25f, 2f);
    }
}

public static class CharacterAiPersonalityUtility
{
    public static float GetActionScoreMultiplier(CharacterActor actor, AIActionSet actionSet)
    {
        CharacterIdentity identity = actor != null ? actor.Identity : null;
        CharacterAiPersonality personality = identity != null && identity.Data != null
            ? identity.Data.aiPersonality
            : null;
        float baseMultiplier = personality != null ? personality.GetActionMultiplier(actionSet) : 1f;
        float runtimeMultiplier = actor != null && actor.PersonaRuntime != null
            ? actor.PersonaRuntime.GetActionMultiplier(actionSet)
            : 1f;
        float configuredMultiplier = Mathf.Clamp(baseMultiplier * runtimeMultiplier, 0.1f, 4f);
        return CharacterMoodImpulseUtility.GetGoodMoodAdherenceMultiplier(
            actor,
            actionSet,
            configuredMultiplier);
    }
}
