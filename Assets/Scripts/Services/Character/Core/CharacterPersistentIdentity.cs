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

public static class CharacterRandomStreamScopeIds
{
    private const string DecisionPrefix = "character-ai:";
    private const string MovementPrefix = "character-movement:";

    public static string Decision(CharacterId characterId) =>
        Build(DecisionPrefix, characterId);

    public static string Movement(CharacterId characterId) =>
        Build(MovementPrefix, characterId);

    private static string Build(string prefix, CharacterId characterId)
    {
        if (!characterId.IsValid)
        {
            throw new ArgumentException(
                "A valid persistent CharacterId is required for a random stream.",
                nameof(characterId));
        }

        return string.Concat(prefix, characterId.Value);
    }
}
