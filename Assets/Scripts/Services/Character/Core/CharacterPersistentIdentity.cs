using System;

public static class CharacterPersistentIdentity
{
    public static CharacterId Require(CharacterActor actor)
    {
        if (actor == null)
        {
            throw new ArgumentNullException(nameof(actor));
        }

        CharacterId id = actor.Identity != null
            ? actor.Identity.TypedPersistentId
            : default;
        if (!id.IsValid)
        {
            throw new InvalidOperationException(
                $"Character '{actor.name}' has no persistent CharacterId.");
        }

        return id;
    }

    public static bool TryGet(CharacterActor actor, out CharacterId id)
    {
        id = actor != null && actor.Identity != null
            ? actor.Identity.TypedPersistentId
            : default;
        return id.IsValid;
    }
}
