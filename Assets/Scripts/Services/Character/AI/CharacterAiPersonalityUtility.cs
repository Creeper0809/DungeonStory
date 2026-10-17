using System;
using UnityEngine;

public static class CharacterAiPersonalityUtility
{

    public static float GetActionMultiplier(this CharacterAiPersonality personality, AIActionSet actionSet)
    {
        if (personality == null)
            throw new NullReferenceException();

        if (actionSet != null && actionSet.HasSemanticTag(CharacterAiActionTags.Work))
        {
            return CharacterAiPersonality.ClampMultiplier(personality.diligence);
        }
        if (actionSet != null && actionSet.HasSemanticTag(CharacterAiActionTags.Curiosity))
        {
            return CharacterAiPersonality.ClampMultiplier(personality.curiosity);
        }
        if (actionSet != null && actionSet.HasSemanticTag(CharacterAiActionTags.SelfCare))
        {
            return CharacterAiPersonality.ClampMultiplier(personality.selfCare);
        }
        if (actionSet != null && actionSet.HasSemanticTag(CharacterAiActionTags.Shopping))
        {
            return CharacterAiPersonality.ClampMultiplier(personality.shoppingInterest);
        }
        if (actionSet != null && actionSet.HasSemanticTag(CharacterAiActionTags.Patience))
        {
            return CharacterAiPersonality.ClampMultiplier(personality.patience);
        }

        return 1f;
    }

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
        CharacterRuntimeProfile effectiveProfile = actor?.Progression != null
            ? actor.Progression.GetEffectiveRuntimeProfile()
            : identity?.Profile;
        float traitMultiplier = effectiveProfile != null
            && actionSet?.Descriptor != null
            ? effectiveProfile.GetBehaviorUtilityMultiplier(
                actionSet.GetSemanticTags(actor))
            : 1f;
        float configuredMultiplier = Mathf.Clamp(
            baseMultiplier * runtimeMultiplier * traitMultiplier,
            0.1f,
            4f);
        return CharacterMoodImpulseUtility.GetGoodMoodAdherenceMultiplier(
            actor,
            actionSet,
            configuredMultiplier);
    }
}
