using System;
using UnityEngine;
using VContainer;

public interface ICharacterFeedbackBubbleFactory
{
    CharacterFeedbackBubble GetOrAdd(CharacterActor actor);
}

public sealed class CharacterFeedbackBubbleFactory : ICharacterFeedbackBubbleFactory
{
    private readonly IObjectResolver objectResolver;

    public CharacterFeedbackBubbleFactory(IObjectResolver objectResolver)
    {
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
    }

    public CharacterFeedbackBubble GetOrAdd(CharacterActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        if (!actor.TryGetComponent(out CharacterFeedbackBubble bubble))
        {
            bubble = actor.gameObject.AddComponent<CharacterFeedbackBubble>();
        }

        objectResolver.Inject(bubble);
        return bubble;
    }
}
