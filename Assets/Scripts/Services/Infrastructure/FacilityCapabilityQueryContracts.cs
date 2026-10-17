using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

public interface IFacilityCapabilityQuery
{
    IReadOnlyList<BuildableObject> FindOperational(
        FacilityCapabilityKind capability,
        string buildingDefinitionId = "");

    IReadOnlyList<BuildableObject> FindOperational(
        ResearchFacilityCommandKind command);
}

