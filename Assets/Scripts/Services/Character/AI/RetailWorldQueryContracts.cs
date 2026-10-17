using System.Collections.Generic;
using DungeonStory.Foundation;

public interface IRetailWorldQuery
{
    int RetailVersion { get; }
    IReadOnlyList<IRetailFacility> RetailFacilities { get; }
}
