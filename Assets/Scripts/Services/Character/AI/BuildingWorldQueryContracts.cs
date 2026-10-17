using System.Collections.Generic;
using DungeonStory.Foundation;

public interface IBuildingWorldQuery
{
    int BuildingVersion { get; }
    IReadOnlyList<BuildableObject> Buildings { get; }
}
