using System.Collections.Generic;
using DungeonStory.Foundation;

public interface ICharacterWorldQuery
{
    int CharacterVersion { get; }
    IReadOnlyList<CharacterActor> Characters { get; }
}

public interface ICharacterLifetimeQuery
{
    int LifetimeCharacterVersion { get; }
    IReadOnlyList<CharacterActor> AllCharacters { get; }
}

