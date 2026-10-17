using System;

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
