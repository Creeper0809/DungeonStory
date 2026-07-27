using System;
using System.Collections.Generic;

public readonly struct AiDirectorContextSceneSnapshot
{
    public AiDirectorContextSceneSnapshot(
        IReadOnlyList<CharacterActor> actors,
        IReadOnlyList<BuildableObject> facilities)
    {
        // Registry views are scene-scoped and consumed synchronously here.
        // Copying every entry made each director tick scale with dungeon size.
        Actors = actors ?? Array.Empty<CharacterActor>();
        Facilities = facilities ?? Array.Empty<BuildableObject>();
    }

    public IReadOnlyList<CharacterActor> Actors { get; }
    public IReadOnlyList<BuildableObject> Facilities { get; }
}

public interface IAiDirectorContextSceneQuery
{
    AiDirectorContextSceneSnapshot Capture();
}

public sealed class AiDirectorContextSceneQuery : IAiDirectorContextSceneQuery
{
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IBuildingWorldQuery buildingWorld;

    public AiDirectorContextSceneQuery(
        ICharacterWorldQuery characterWorld,
        IBuildingWorldQuery buildingWorld)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
    }

    public AiDirectorContextSceneSnapshot Capture()
    {
        return new AiDirectorContextSceneSnapshot(
            characterWorld.Characters,
            buildingWorld.Buildings);
    }
}
