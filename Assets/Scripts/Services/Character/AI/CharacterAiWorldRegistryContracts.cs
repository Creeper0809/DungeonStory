using System.Collections.Generic;
using DungeonStory.Foundation;

public interface ICharacterAiWorldRegistry :
    ICharacterWorldQuery,
    ICharacterLifetimeQuery,
    IWildlifeWorldQuery,
    IBuildingWorldQuery,
    IWarehouseWorldQuery,
    IRetailWorldQuery
{
    int Version { get; }

    void RegisterCharacter(CharacterActor actor);
    void UnregisterCharacter(CharacterActor actor);
    void RegisterCharacterLifetime(CharacterActor actor);
    void UnregisterCharacterLifetime(CharacterActor actor);
    void RegisterWildlife(WildlifeActor actor);
    void UnregisterWildlife(WildlifeActor actor);
    void RegisterBuilding(BuildableObject building);
    void UnregisterBuilding(BuildableObject building);
    int ReleaseTransientBuildingOwnership(
        IBuildingVisitorPort visitor,
        string reason);
    int GetTransientBuildingOwnershipCount(CharacterId characterId);
    void RegisterWarehouse(IWarehouseFacility warehouse);
    void UnregisterWarehouse(IWarehouseFacility warehouse);
    void SetGrid(Grid grid);
    bool TryGetGrid(out Grid grid);
    bool TryGetSessionState(out GameSessionState data);
    void Clear();
}
