using System.Collections.Generic;
using DungeonStory.Foundation;

public interface IWildlifeWorldQuery
{
    int WildlifeVersion { get; }
    IReadOnlyList<WildlifeActor> Wildlife { get; }
}
