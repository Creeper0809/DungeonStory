using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Characters;

public sealed class CharacterIdRegistryAdapter : ICharacterIdRegistry
{
    private readonly ICharacterLifetimeQuery characters;
    private readonly DungeonStory.Characters.CharacterIdRegistry registry;

    public CharacterIdRegistryAdapter(
        ICharacterLifetimeQuery characters,
        DungeonStory.Characters.CharacterIdRegistry registry)
    {
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.registry = registry
            ?? throw new ArgumentNullException(nameof(registry));
    }

    public bool TryGetPersistentId(
        CharacterActor actor,
        out string persistentId)
    {
        actor = CharacterActorCollection.GetCanonical(actor);
        if (actor == null)
        {
            persistentId = string.Empty;
            return false;
        }

        CharacterIdentitySceneAdapter identity = new(actor, characters);
        bool found = registry.TryGetPersistentId(identity, out CharacterId id);
        persistentId = found ? id.Value : string.Empty;
        return found;
    }

    public string GetOrAssignPersistentId(CharacterActor actor)
    {
        actor = CharacterActorCollection.GetCanonical(actor);
        if (actor == null)
        {
            throw new ArgumentNullException(nameof(actor));
        }

        return registry.GetOrAssignPersistentId(
            new CharacterIdentitySceneAdapter(actor, characters)).Value;
    }
}

internal sealed class CharacterIdentitySceneAdapter : ICharacterIdentityRegistryPort
{
    private readonly CharacterActor actor;
    private readonly ICharacterLifetimeQuery characters;

    public CharacterIdentitySceneAdapter(
        CharacterActor actor,
        ICharacterLifetimeQuery characters)
    {
        this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
    }

    public CharacterIdentitySnapshot CaptureIdentity()
    {
        CharacterIdentity identity = actor.Identity;
        return new CharacterIdentitySnapshot(
            identity != null,
            identity != null && identity.IsOwner,
            (CharacterId)(identity != null
                ? identity.PersistentId
                : string.Empty));
    }

    public IReadOnlyCollection<CharacterId> CaptureAssignedIds()
    {
        return characters.AllCharacters
            .Where(value => value != null && value.Identity != null)
            .Select(value => (CharacterId)value.Identity.PersistentId)
            .Where(value => value.IsValid)
            .ToArray();
    }

    public void EnsureRuntimeState() => actor.EnsureRuntimeState();

    public void AssignPersistentId(CharacterId persistentId)
    {
        CharacterIdentity identity = actor.Identity
            ?? throw new InvalidOperationException(
                "CharacterActor requires CharacterIdentity.");
        identity.SetPersistentId(persistentId);
    }
}
