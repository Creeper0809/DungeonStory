using System;
using System.Collections.Generic;
using System.Linq;
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
    bool TryGetSessionState(out GameSessionState data);
    void Clear();
}

public sealed class CharacterAiWorldRegistry :
    ICharacterAiWorldRegistry,
    IBuildingWorldRegistryPort,
    IBuildingCharacterDisplayQuery
{
    private readonly ISceneRuntimeRegistry<CharacterActor> characters;
    private readonly ISceneRuntimeRegistry<CharacterActor> lifetimeCharacters =
        new SceneRuntimeRegistry<CharacterActor>();
    private readonly ISceneRuntimeRegistry<WildlifeActor> wildlife;
    private readonly ISceneRuntimeRegistry<BuildableObject> buildings;
    private readonly ISceneRuntimeRegistry<IWarehouseFacility> warehouses;
    private readonly ISceneRuntimeRegistry<IRetailFacility> retailFacilities;
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly IRestoreWorldCandidateQuery restoreCandidates;
    private readonly ICharacterLifePublicationService lifePublication;
    private Grid grid;
    private int gridVersion;

    public CharacterAiWorldRegistry(
        ISceneRuntimeRegistry<CharacterActor> characters,
        ISceneRuntimeRegistry<WildlifeActor> wildlife,
        ISceneRuntimeRegistry<BuildableObject> buildings,
        ISceneRuntimeRegistry<IWarehouseFacility> warehouses,
        ISceneRuntimeRegistry<IRetailFacility> retailFacilities,
        IGameSessionStateProvider gameDataProvider,
        IRestoreWorldCandidateQuery restoreCandidates,
        ICharacterLifePublicationService lifePublication)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.warehouses = warehouses ?? throw new ArgumentNullException(nameof(warehouses));
        this.retailFacilities = retailFacilities
            ?? throw new ArgumentNullException(nameof(retailFacilities));
        this.gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.restoreCandidates = restoreCandidates
            ?? throw new ArgumentNullException(nameof(restoreCandidates));
        this.lifePublication = lifePublication
            ?? throw new ArgumentNullException(nameof(lifePublication));
    }

    public int Version => unchecked(
        gridVersion
        + characters.Version
        + lifetimeCharacters.Version
        + wildlife.Version
        + buildings.Version
        + warehouses.Version
        + retailFacilities.Version
        + restoreCandidates.Revision);
    public int CharacterVersion => unchecked(
        characters.Version + restoreCandidates.Revision);
    public int LifetimeCharacterVersion => unchecked(
        lifetimeCharacters.Version + restoreCandidates.Revision);
    public int WildlifeVersion => unchecked(
        wildlife.Version + restoreCandidates.Revision);
    public int BuildingVersion => unchecked(
        buildings.Version + restoreCandidates.Revision);
    public int WarehouseVersion => unchecked(
        warehouses.Version + restoreCandidates.Revision);
    public int RetailVersion => unchecked(
        retailFacilities.Version + restoreCandidates.Revision);
    public IReadOnlyList<CharacterActor> Characters =>
        restoreCandidates.TryGetCharacters(out IReadOnlyList<CharacterActor> candidates)
            ? candidates
            : characters.Entries;
    public IReadOnlyList<CharacterActor> AllCharacters =>
        restoreCandidates.TryGetCharacters(out IReadOnlyList<CharacterActor> candidates)
            ? candidates
            : lifetimeCharacters.Entries;
    public IReadOnlyList<WildlifeActor> Wildlife =>
        restoreCandidates.TryGetWildlife(out IReadOnlyList<WildlifeActor> candidates)
            ? candidates
            : wildlife.Entries;
    public IReadOnlyList<BuildableObject> Buildings =>
        restoreCandidates.TryGetBuildings(out IReadOnlyList<BuildableObject> candidates)
            ? candidates
            : buildings.Entries;
    IReadOnlyList<IBuildingWorldEntryPort> IBuildingWorldRegistryPort.Buildings => Buildings;
    public IReadOnlyList<IWarehouseFacility> Warehouses =>
        restoreCandidates.TryGetBuildings(out IReadOnlyList<BuildableObject> candidates)
            ? FilterCandidateFacilities<IWarehouseFacility>(candidates)
            : warehouses.Entries;
    public IReadOnlyList<IRetailFacility> RetailFacilities =>
        restoreCandidates.TryGetBuildings(out IReadOnlyList<BuildableObject> candidates)
            ? FilterCandidateFacilities<IRetailFacility>(candidates)
            : retailFacilities.Entries;

    public bool TryGetDisplayName(
        string persistentId,
        out string displayName)
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            throw new ArgumentException(
                "Building character display lookup requires a persistent id.",
                nameof(persistentId));
        }

        CharacterActor actor = Characters.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.Identity?.PersistentId,
                persistentId,
                StringComparison.Ordinal));
        displayName = actor?.Identity?.DisplayName ?? string.Empty;
        return !string.IsNullOrWhiteSpace(displayName);
    }

    public void RegisterCharacter(CharacterActor actor)
    {
        CharacterActor canonical = CharacterActorCollection.GetCanonical(actor);
        if (canonical != null)
        {
            characters.Register(canonical);
        }
    }

    public void UnregisterCharacter(CharacterActor actor)
    {
        CharacterActor canonical = CharacterActorCollection.GetCanonical(actor);
        if (canonical != null)
        {
            characters.Unregister(canonical);
        }
    }

    public void RegisterCharacterLifetime(CharacterActor actor)
    {
        CharacterActor canonical = CharacterActorCollection.GetCanonical(actor);
        if (canonical != null && lifetimeCharacters.Register(canonical))
        {
            try
            {
                lifePublication.EnsureRegistered(canonical);
            }
            catch
            {
                lifetimeCharacters.Unregister(canonical);
                throw;
            }
        }
    }

    public void UnregisterCharacterLifetime(CharacterActor actor)
    {
        CharacterActor canonical = CharacterActorCollection.GetCanonical(actor);
        if (canonical != null)
        {
            lifetimeCharacters.Unregister(canonical);
        }
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

    void IBuildingWorldRegistryPort.RegisterBuilding(IBuildingWorldEntryPort building)
    {
        if (building == null)
        {
            return;
        }

        if (building is not BuildableObject buildableObject)
        {
            throw new ArgumentException(
                $"{nameof(IBuildingWorldRegistryPort)} only accepts {nameof(BuildableObject)} entries.",
                nameof(building));
        }

        RegisterBuilding(buildableObject);
    }

    void IBuildingWorldRegistryPort.UnregisterBuilding(IBuildingWorldEntryPort building)
    {
        if (building == null)
        {
            return;
        }

        if (building is not BuildableObject buildableObject)
        {
            throw new ArgumentException(
                $"{nameof(IBuildingWorldRegistryPort)} only accepts {nameof(BuildableObject)} entries.",
                nameof(building));
        }

        UnregisterBuilding(buildableObject);
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
        if (restoreCandidates.TryGetGrid(out grid))
        {
            return true;
        }

        grid = this.grid;
        return grid != null;
    }

    public bool TryGetSessionState(out GameSessionState data)
    {
        return gameDataProvider.TryGetSessionState(out data);
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

    private static IReadOnlyList<TFacility> FilterCandidateFacilities<TFacility>(
        IReadOnlyList<BuildableObject> candidates)
        where TFacility : class
    {
        List<TFacility> result = new List<TFacility>();
        foreach (BuildableObject candidate in
                 candidates ?? Array.Empty<BuildableObject>())
        {
            if (candidate is TFacility facility)
            {
                result.Add(facility);
            }
        }

        return result;
    }
}
