using System.Collections.Generic;
using DungeonStory.Foundation;

public interface IBuildingWorldEntryPort
{
    BuildingInstanceId BuildingInstanceId { get; }
    bool IsBuildingDestroyed { get; }
}

public interface IBuildingWorldRegistryPort
{
    int BuildingVersion { get; }
    IReadOnlyList<IBuildingWorldEntryPort> Buildings { get; }
    void RegisterBuilding(IBuildingWorldEntryPort building);
    void UnregisterBuilding(IBuildingWorldEntryPort building);
}

public interface IBuildingFacilityStateChangePort
{
    void MarkDynamicStateDirty();
}

public interface IBuildingCharacterPort
{
    CharacterId BuildingCharacterId { get; }
    string BuildingDisplayName { get; }
    bool IsBuildingInteractionAvailable { get; }
}
