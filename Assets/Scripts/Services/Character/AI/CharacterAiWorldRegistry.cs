using System;
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

public interface IWildlifeWorldQuery
{
    int WildlifeVersion { get; }
    IReadOnlyList<WildlifeActor> Wildlife { get; }
}

public interface IBuildingWorldQuery
{
    int BuildingVersion { get; }
    IReadOnlyList<BuildableObject> Buildings { get; }
}

public interface IWarehouseWorldQuery
{
    int WarehouseVersion { get; }
    IReadOnlyList<IWarehouseFacility> Warehouses { get; }
}

public interface IRetailWorldQuery
{
    int RetailVersion { get; }
    IReadOnlyList<IRetailFacility> RetailFacilities { get; }
}

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
    void RegisterWarehouse(IWarehouseFacility warehouse);
    void UnregisterWarehouse(IWarehouseFacility warehouse);
    void SetGrid(Grid grid);
    bool TryGetGrid(out Grid grid);
    bool TryGetGameData(out GameData data);
    void Clear();
}

public sealed class CharacterAiWorldRegistry : ICharacterAiWorldRegistry
{
    private readonly ISceneRuntimeRegistry<CharacterActor> characters;
    private readonly ISceneRuntimeRegistry<CharacterActor> lifetimeCharacters =
        new SceneRuntimeRegistry<CharacterActor>();
    private readonly ISceneRuntimeRegistry<WildlifeActor> wildlife;
    private readonly ISceneRuntimeRegistry<BuildableObject> buildings;
    private readonly ISceneRuntimeRegistry<IWarehouseFacility> warehouses;
    private readonly ISceneRuntimeRegistry<IRetailFacility> retailFacilities;
    private readonly IGameDataProvider gameDataProvider;
    private Grid grid;
    private int gridVersion;

    public CharacterAiWorldRegistry(
        ISceneRuntimeRegistry<CharacterActor> characters,
        ISceneRuntimeRegistry<WildlifeActor> wildlife,
        ISceneRuntimeRegistry<BuildableObject> buildings,
        ISceneRuntimeRegistry<IWarehouseFacility> warehouses,
        ISceneRuntimeRegistry<IRetailFacility> retailFacilities,
        IGameDataProvider gameDataProvider)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.warehouses = warehouses ?? throw new ArgumentNullException(nameof(warehouses));
        this.retailFacilities = retailFacilities
            ?? throw new ArgumentNullException(nameof(retailFacilities));
        this.gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
    }

    public int Version => unchecked(
        gridVersion
        + characters.Version
        + lifetimeCharacters.Version
        + wildlife.Version
        + buildings.Version
        + warehouses.Version
        + retailFacilities.Version);
    public int CharacterVersion => characters.Version;
    public int LifetimeCharacterVersion => lifetimeCharacters.Version;
    public int WildlifeVersion => wildlife.Version;
    public int BuildingVersion => buildings.Version;
    public int WarehouseVersion => warehouses.Version;
    public int RetailVersion => retailFacilities.Version;
    public IReadOnlyList<CharacterActor> Characters => characters.Entries;
    public IReadOnlyList<CharacterActor> AllCharacters =>
        lifetimeCharacters.Entries;
    public IReadOnlyList<WildlifeActor> Wildlife => wildlife.Entries;
    public IReadOnlyList<BuildableObject> Buildings => buildings.Entries;
    public IReadOnlyList<IWarehouseFacility> Warehouses => warehouses.Entries;
    public IReadOnlyList<IRetailFacility> RetailFacilities => retailFacilities.Entries;

    public void RegisterCharacter(CharacterActor actor)
    {
        characters.Register(actor);
    }

    public void UnregisterCharacter(CharacterActor actor)
    {
        characters.Unregister(actor);
    }

    public void RegisterCharacterLifetime(CharacterActor actor)
    {
        lifetimeCharacters.Register(actor);
    }

    public void UnregisterCharacterLifetime(CharacterActor actor)
    {
        lifetimeCharacters.Unregister(actor);
    }

    public void RegisterWildlife(WildlifeActor actor)
    {
        wildlife.Register(actor);
    }

    public void UnregisterWildlife(WildlifeActor actor)
    {
        wildlife.Unregister(actor);
    }

    public void RegisterBuilding(BuildableObject building)
    {
        if (building == null)
        {
            return;
        }

        buildings.Register(building);
        if (building is IWarehouseFacility warehouse)
        {
            warehouses.Register(warehouse);
        }
        if (building is IRetailFacility retailFacility)
        {
            retailFacilities.Register(retailFacility);
        }
    }

    public void UnregisterBuilding(BuildableObject building)
    {
        if (building == null)
        {
            return;
        }

        buildings.Unregister(building);
        if (building is IWarehouseFacility warehouse)
        {
            warehouses.Unregister(warehouse);
        }
        if (building is IRetailFacility retailFacility)
        {
            retailFacilities.Unregister(retailFacility);
        }
    }

    public void RegisterWarehouse(IWarehouseFacility warehouse)
    {
        warehouses.Register(warehouse);
    }

    public void UnregisterWarehouse(IWarehouseFacility warehouse)
    {
        warehouses.Unregister(warehouse);
    }

    public void SetGrid(Grid grid)
    {
        if (ReferenceEquals(this.grid, grid))
        {
            return;
        }

        this.grid = grid;
        unchecked
        {
            gridVersion++;
        }
    }

    public bool TryGetGrid(out Grid grid)
    {
        grid = this.grid;
        return grid != null;
    }

    public bool TryGetGameData(out GameData data)
    {
        return gameDataProvider.TryGetGameData(out data);
    }

    public void Clear()
    {
        characters.Clear();
        lifetimeCharacters.Clear();
        wildlife.Clear();
        buildings.Clear();
        warehouses.Clear();
        retailFacilities.Clear();
        grid = null;
        unchecked
        {
            gridVersion++;
        }
    }
}
