using System;
using UnityEngine;
using VContainer;

public interface ICharacterSocialMemoryFactory
{
    CharacterSocialMemory GetOrAdd(CharacterActor actor);
}

public sealed class CharacterSocialMemoryFactory : ICharacterSocialMemoryFactory
{
    private readonly IObjectResolver objectResolver;

    public CharacterSocialMemoryFactory(IObjectResolver objectResolver)
    {
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
    }

    public CharacterSocialMemory GetOrAdd(CharacterActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        if (!actor.TryGetComponent(out CharacterSocialMemory memory))
        {
            memory = actor.gameObject.AddComponent<CharacterSocialMemory>();
        }

        objectResolver.Inject(memory);
        memory.Bind(actor);
        return memory;
    }
}
