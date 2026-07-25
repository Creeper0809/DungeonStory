using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct AiDirectorContextSceneSnapshot
{
    public AiDirectorContextSceneSnapshot(
        IReadOnlyList<CharacterActor> actors,
        IReadOnlyList<BuildableObject> facilities)
    {
        Actors = EventPayloadSnapshot.Copy(actors);
        Facilities = EventPayloadSnapshot.Copy(facilities);
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
            characterWorld.Characters.ToArray(),
            buildingWorld.Buildings.ToArray());
    }
}
