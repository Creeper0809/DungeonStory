using UnityEngine;

internal struct CharacterAiDecisionCadenceSettings
{
    public float RegistrationSpreadSeconds;
    public float OwnerDecisionInterval;
    public float VisibleDecisionInterval;
    public float OffscreenDecisionInterval;
    public float DecisionIntervalJitterRatio;
    public float ViewportMargin;
    public int OffscreenMovementFrameStride;
}

internal sealed class CharacterAiDecisionCadencePolicy
{
    public int GetMovementFrameStride(
        CharacterActor actor,
        bool schedulerEnabled,
        Camera camera,
        CharacterAiDecisionCadenceSettings settings)
    {
        if (!schedulerEnabled
            || settings.OffscreenMovementFrameStride <= 1
            || IsHighDetailCharacter(actor, camera, settings))
        {
            return 1;
        }

        return settings.OffscreenMovementFrameStride;
    }

    public float GetNextDecisionInterval(
        CharacterActor actor,
        Camera camera,
        CharacterAiDecisionCadenceSettings settings)
    {
        float interval;
        if (actor != null && actor.IsOwner)
        {
            interval = settings.OwnerDecisionInterval;
        }
        else
        {
            interval = IsHighDetailCharacter(actor, camera, settings)
                ? settings.VisibleDecisionInterval
                : settings.OffscreenDecisionInterval;
        }

        return interval * ResolveActorIntervalJitter(actor, settings);
    }

    public float GetRegistrationDelay(
        CharacterActor actor,
        Camera camera,
        bool hasCameraProvider,
        CharacterAiDecisionCadenceSettings settings)
    {
        float interval = actor != null && actor.IsOwner
            ? settings.OwnerDecisionInterval
            : hasCameraProvider
                ? GetNextDecisionInterval(actor, camera, settings)
                : settings.OffscreenDecisionInterval;
        float spread = Mathf.Min(
            Mathf.Max(0f, settings.RegistrationSpreadSeconds),
            interval);
        return spread * ResolveActorStableFraction(actor);
    }

    public bool IsHighDetailCharacter(
        CharacterActor actor,
        Camera camera,
        CharacterAiDecisionCadenceSettings settings)
    {
        if (actor == null)
        {
            return false;
        }

        if (actor.IsOwner || camera == null)
        {
            return true;
        }

        Vector3 viewport = camera.WorldToViewportPoint(actor.transform.position);
        return viewport.z >= 0f
            && viewport.x >= -settings.ViewportMargin
            && viewport.x <= 1f + settings.ViewportMargin
            && viewport.y >= -settings.ViewportMargin
            && viewport.y <= 1f + settings.ViewportMargin;
    }

    private static float ResolveActorIntervalJitter(
        CharacterActor actor,
        CharacterAiDecisionCadenceSettings settings)
    {
        if (actor == null || settings.DecisionIntervalJitterRatio <= 0f)
        {
            return 1f;
        }

        float fraction = ResolveActorStableFraction(actor);
        return Mathf.Lerp(
            1f - settings.DecisionIntervalJitterRatio,
            1f + settings.DecisionIntervalJitterRatio,
            fraction);
    }

    private static float ResolveActorStableFraction(CharacterActor actor)
    {
        if (actor == null)
        {
            return 0f;
        }

        CharacterId characterId = CharacterPersistentIdentity.Require(actor);
        return PersistentEntityId.GetStableUnitFraction(characterId);
    }
}
